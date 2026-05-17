using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class TcpService
{
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<string, TcpListener> _servers = new();
    private readonly ConcurrentDictionary<string, TcpConnectionInfo> _connectionInfos = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _connectionCts = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, NetworkStream>> _serverClients = new();
    private readonly ConcurrentDictionary<string, Action<byte[]>> _dataHandlers = new();

    public event Action<string, TcpConnectionStatus>? ConnectionStatusChanged;
    public event Action<string, byte[]>? DataReceived;

    public Task<List<TcpConnectionInfo>> GetAllConnectionsAsync()
    {
        return Task.FromResult(_connectionInfos.Values.ToList());
    }

    public Task<TcpConnectionInfo?> GetConnectionAsync(string connectionId)
    {
        _connectionInfos.TryGetValue(connectionId, out var info);
        return Task.FromResult(info);
    }

    public async Task<string> ConnectAsync(TcpConfig config, string connectionName = "")
    {
        var connectionId = Guid.NewGuid().ToString();
        var name = string.IsNullOrEmpty(connectionName) ? $"TCP-{connectionId}" : connectionName;

        var connectionInfo = new TcpConnectionInfo
        {
            Id = connectionId,
            Name = name,
            Host = config.Host,
            Port = config.Port,
            IsServer = config.IsServer,
            Status = config.IsServer ? TcpConnectionStatus.Listening : TcpConnectionStatus.Connecting
        };

        _connectionInfos[connectionId] = connectionInfo;
        var cts = new CancellationTokenSource();
        _connectionCts[connectionId] = cts;

        if (config.IsServer)
        {
            await StartServerAsync(connectionId, config, cts.Token);
        }
        else
        {
            await ConnectClientAsync(connectionId, config, cts.Token);
        }

        return connectionId;
    }

    private async Task StartServerAsync(string connectionId, TcpConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Parse(config.Host), config.Port);
            listener.Start();
            _servers[connectionId] = listener;

            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.Status = TcpConnectionStatus.Listening;
                info.ConnectedAt = DateTime.UtcNow;
                info.ClientIds = new List<string>();
            }

            ConnectionStatusChanged?.Invoke(connectionId, TcpConnectionStatus.Listening);

            _ = AcceptClientsAsync(connectionId, listener, config, cancellationToken);
        }
        catch (Exception ex)
        {
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.Status = TcpConnectionStatus.Error;
                info.ErrorCount++;
            }
            ConnectionStatusChanged?.Invoke(connectionId, TcpConnectionStatus.Error);
            Console.WriteLine($"TCP Server start error: {ex.Message}");
        }
    }

    private async Task AcceptClientsAsync(string serverId, TcpListener listener, TcpConfig config, CancellationToken cancellationToken)
    {
        var serverClientStreams = new ConcurrentDictionary<string, NetworkStream>();
        _serverClients[serverId] = serverClientStreams;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync();
                var clientId = Guid.NewGuid().ToString();
                serverClientStreams[clientId] = client.GetStream();

                if (_connectionInfos.TryGetValue(serverId, out var info) && info.ClientIds != null)
                {
                    info.ClientIds.Add(clientId);
                }

                _ = ReceiveDataFromClientAsync(serverId, clientId, client, config, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_connectionInfos.TryGetValue(serverId, out var info))
                {
                    info.ErrorCount++;
                }
                Console.WriteLine($"Accept client error: {ex.Message}");
            }
        }
    }

    private async Task ReceiveDataFromClientAsync(string serverId, string clientId, TcpClient client, TcpConfig config, CancellationToken cancellationToken)
    {
        var buffer = new byte[config.ReceiveBufferSize];
        var stream = client.GetStream();
        var sb = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0) break;

                if (_connectionInfos.TryGetValue(serverId, out var info))
                {
                    info.BytesReceived += bytesRead;
                    info.LastDataAt = DateTime.UtcNow;
                }

                if (config.UseDelimiter)
                {
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    var content = sb.ToString();
                    var delimiter = config.Delimiter;

                    while (content.Contains(delimiter))
                    {
                        var delimiterIndex = content.IndexOf(delimiter);
                        var message = content.Substring(0, delimiterIndex);
                        var messageBytes = Encoding.UTF8.GetBytes(message);

                        if (_connectionInfos.TryGetValue(serverId, out var infoMsg))
                        {
                            infoMsg.MessagesReceived++;
                        }

                        if (_dataHandlers.TryGetValue(serverId, out var handler))
                        {
                            handler(messageBytes);
                        }
                        DataReceived?.Invoke(serverId, messageBytes);

                        content = content.Substring(delimiterIndex + delimiter.Length);
                    }
                    sb.Clear();
                    sb.Append(content);
                }
                else
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    if (_connectionInfos.TryGetValue(serverId, out var infoMsg))
                    {
                        infoMsg.MessagesReceived++;
                    }

                    if (_dataHandlers.TryGetValue(serverId, out var handler))
                    {
                        handler(data);
                    }
                    DataReceived?.Invoke(serverId, data);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_connectionInfos.TryGetValue(serverId, out var info))
            {
                info.ErrorCount++;
            }
            Console.WriteLine($"Receive data error: {ex.Message}");
        }
        finally
        {
            _serverClients.TryGetValue(serverId, out var streams);
            streams?.TryRemove(clientId, out _);
            client.Dispose();
        }
    }

    private async Task ConnectClientAsync(string connectionId, TcpConfig config, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (attempt < config.MaxReconnectAttempts || config.AutoReconnect)
        {
            try
            {
                var client = new TcpClient();
                await client.ConnectAsync(config.Host, config.Port);
                _clients[connectionId] = client;

                if (_connectionInfos.TryGetValue(connectionId, out var info))
                {
                    info.Status = TcpConnectionStatus.Connected;
                    info.ConnectedAt = DateTime.UtcNow;
                }

                ConnectionStatusChanged?.Invoke(connectionId, TcpConnectionStatus.Connected);

                _ = ReceiveDataAsync(connectionId, client, config, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                attempt++;
                if (_connectionInfos.TryGetValue(connectionId, out var info))
                {
                    info.ErrorCount++;
                    info.Status = TcpConnectionStatus.Connecting;
                }

                Console.WriteLine($"TCP connection attempt {attempt} failed: {ex.Message}");

                if (attempt >= config.MaxReconnectAttempts && !config.AutoReconnect)
                {
                    if (_connectionInfos.TryGetValue(connectionId, out var infoErr))
                    {
                        infoErr.Status = TcpConnectionStatus.Error;
                    }
                    ConnectionStatusChanged?.Invoke(connectionId, TcpConnectionStatus.Error);
                    break;
                }

                await Task.Delay(config.ReconnectInterval, cancellationToken);
            }
        }
    }

    private async Task ReceiveDataAsync(string connectionId, TcpClient client, TcpConfig config, CancellationToken cancellationToken)
    {
        var buffer = new byte[config.ReceiveBufferSize];
        var stream = client.GetStream();
        var sb = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead == 0) break;

                if (_connectionInfos.TryGetValue(connectionId, out var info))
                {
                    info.BytesReceived += bytesRead;
                    info.LastDataAt = DateTime.UtcNow;
                }

                if (config.UseDelimiter)
                {
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    var content = sb.ToString();
                    var delimiter = config.Delimiter;

                    while (content.Contains(delimiter))
                    {
                        var delimiterIndex = content.IndexOf(delimiter);
                        var message = content.Substring(0, delimiterIndex);
                        var messageBytes = Encoding.UTF8.GetBytes(message);

                        if (_connectionInfos.TryGetValue(connectionId, out var infoMsg))
                        {
                            infoMsg.MessagesReceived++;
                        }

                        if (_dataHandlers.TryGetValue(connectionId, out var handler))
                        {
                            handler(messageBytes);
                        }
                        DataReceived?.Invoke(connectionId, messageBytes);

                        content = content.Substring(delimiterIndex + delimiter.Length);
                    }
                    sb.Clear();
                    sb.Append(content);
                }
                else
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    if (_connectionInfos.TryGetValue(connectionId, out var infoMsg))
                    {
                        infoMsg.MessagesReceived++;
                    }

                    if (_dataHandlers.TryGetValue(connectionId, out var handler))
                    {
                        handler(data);
                    }
                    DataReceived?.Invoke(connectionId, data);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.ErrorCount++;
            }
            Console.WriteLine($"TCP receive error: {ex.Message}");
        }
        finally
        {
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.Status = TcpConnectionStatus.Disconnected;
            }
            _clients.TryRemove(connectionId, out _);
            ConnectionStatusChanged?.Invoke(connectionId, TcpConnectionStatus.Disconnected);

            if (config.AutoReconnect)
            {
                _ = ReconnectAsync(connectionId, config);
            }
        }
    }

    private async Task ReconnectAsync(string connectionId, TcpConfig config)
    {
        await Task.Delay(config.ReconnectInterval);
        if (_connectionCts.TryGetValue(connectionId, out var cts) && !cts.IsCancellationRequested)
        {
            await ConnectClientAsync(connectionId, config, cts.Token);
        }
    }

    public async Task<bool> SendAsync(string connectionId, byte[] data)
    {
        try
        {
            if (_servers.TryGetValue(connectionId, out _))
            {
                if (_serverClients.TryGetValue(connectionId, out var streams))
                {
                    var success = true;
                    foreach (var stream in streams.Values)
                    {
                        try
                        {
                            await stream.WriteAsync(data, 0, data.Length);
                            await stream.FlushAsync();

                            if (_connectionInfos.TryGetValue(connectionId, out var info))
                            {
                                info.BytesSent += data.Length;
                                info.MessagesSent++;
                            }
                        }
                        catch
                        {
                            success = false;
                        }
                    }
                    return success;
                }
                return false;
            }
            else if (_clients.TryGetValue(connectionId, out var client))
            {
                var stream = client.GetStream();
                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();

                if (_connectionInfos.TryGetValue(connectionId, out var info))
                {
                    info.BytesSent += data.Length;
                    info.MessagesSent++;
                }
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.ErrorCount++;
            }
            Console.WriteLine($"TCP send error: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (_connectionCts.TryGetValue(connectionId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _connectionCts.TryRemove(connectionId, out _);
        }

        if (_servers.TryGetValue(connectionId, out var listener))
        {
            try
            {
                listener.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Stop listener error: {ex.Message}");
            }
            _servers.TryRemove(connectionId, out _);

            if (_serverClients.TryGetValue(connectionId, out var streams))
            {
                foreach (var stream in streams.Values)
                {
                    stream.Dispose();
                }
                _serverClients.TryRemove(connectionId, out _);
            }
        }

        if (_clients.TryGetValue(connectionId, out var client))
        {
            client.Dispose();
            _clients.TryRemove(connectionId, out _);
        }

        if (_connectionInfos.TryGetValue(connectionId, out var info))
        {
            info.Status = TcpConnectionStatus.Disconnected;
        }
        _dataHandlers.TryRemove(connectionId, out _);

        ConnectionStatusChanged?.Invoke(connectionId, TcpConnectionStatus.Disconnected);
    }

    public Task RegisterDataHandlerAsync(string connectionId, Action<byte[]> handler)
    {
        _dataHandlers[connectionId] = handler;
        return Task.CompletedTask;
    }
}
