using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class EnhancedTcpService : IDisposable
{
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();
    private readonly ConcurrentDictionary<string, TcpListener> _servers = new();
    private readonly ConcurrentDictionary<string, TcpConnectionInfo> _connectionInfos = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _connectionCts = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, NetworkStream>> _serverClients = new();
    private readonly ConcurrentDictionary<string, Action<byte[]>> _dataHandlers = new();
    private readonly ConcurrentDictionary<string, Channel<byte[]>> _receiveQueues = new();
    
    private readonly ConnectionPool<TcpClient> _connectionPool;
    private readonly BackpressureController _backpressureController;
    private readonly CommunicationPerformanceMonitor _performanceMonitor;
    
    private readonly int _defaultBufferSize;
    private readonly int _maxConnections;
    
    private bool _disposed;

    public EnhancedTcpService(
        ConnectionPool<TcpClient>? connectionPool = null,
        BackpressureController? backpressureController = null,
        CommunicationPerformanceMonitor? performanceMonitor = null)
    {
        _connectionPool = connectionPool ?? new ConnectionPool<TcpClient>(
            _ => new TcpClient(),
            client => { try { client.Dispose(); } catch { } },
            50
        );
        _backpressureController = backpressureController ?? new BackpressureController();
        _performanceMonitor = performanceMonitor ?? new CommunicationPerformanceMonitor();
        
        _defaultBufferSize = 8192;
        _maxConnections = 100;
    }

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
        _performanceMonitor.RecordConnectionOpen("TCP", connectionId);
        
        var cts = new CancellationTokenSource();
        _connectionCts[connectionId] = cts;
        
        var queue = _backpressureController.GetOrCreateQueue(connectionId);
        _receiveQueues[connectionId] = queue;

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
            _performanceMonitor.RecordConnectionOpen("TCP-Server", connectionId);

            _ = AcceptClientsAsync(connectionId, listener, config, cancellationToken);
        }
        catch (Exception ex)
        {
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.Status = TcpConnectionStatus.Error;
                info.ErrorCount++;
            }
            _performanceMonitor.RecordError("TCP-Server");
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
                if (_connectionInfos.TryGetValue(serverId, out var serverInfo) && 
                    serverInfo.ClientIds != null && 
                    serverInfo.ClientIds.Count >= _maxConnections)
                {
                    await Task.Delay(100, cancellationToken);
                    continue;
                }
                
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
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
                _performanceMonitor.RecordError("TCP-Server");
                Console.WriteLine($"Accept client error: {ex.Message}");
            }
        }
    }

    private async Task ReceiveDataFromClientAsync(string serverId, string clientId, TcpClient client, TcpConfig config, CancellationToken cancellationToken)
    {
        var buffer = new byte[config.ReceiveBufferSize > 0 ? config.ReceiveBufferSize : _defaultBufferSize];
        var stream = client.GetStream();
        var messageBuffer = new byte[buffer.Length * 10];
        var messageLength = 0;
        var stopwatch = new Stopwatch();

        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                stopwatch.Restart();
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                stopwatch.Stop();
                
                if (bytesRead == 0) break;

                _performanceMonitor.RecordMessageReceived("TCP-Server", bytesRead);
                
                if (_connectionInfos.TryGetValue(serverId, out var info))
                {
                    info.BytesReceived += bytesRead;
                    info.LastDataAt = DateTime.UtcNow;
                }

                if (config.UseDelimiter && !string.IsNullOrEmpty(config.Delimiter))
                {
                    await ProcessDelimitedMessagesAsync(serverId, buffer, bytesRead, config.Delimiter, stopwatch.Elapsed.TotalMilliseconds);
                }
                else
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    await ProcessMessageAsync(serverId, data, stopwatch.Elapsed.TotalMilliseconds);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _performanceMonitor.RecordError("TCP-Server");
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

    private async Task ProcessDelimitedMessagesAsync(string serverId, byte[] buffer, int bytesRead, string delimiter, double latencyMs)
    {
        if (_receiveQueues.TryGetValue(serverId, out var queue))
        {
            var delimiterBytes = Encoding.UTF8.GetBytes(delimiter);
            
            for (var i = 0; i < bytesRead; i++)
            {
                var shouldProcess = false;
                if (i + delimiterBytes.Length <= bytesRead)
                {
                    shouldProcess = true;
                    for (var j = 0; j < delimiterBytes.Length; j++)
                    {
                        if (buffer[i + j] != delimiterBytes[j])
                        {
                            shouldProcess = false;
                            break;
                        }
                    }
                }
                
                if (shouldProcess)
                {
                    var data = new byte[i];
                    Array.Copy(buffer, data, i);
                    await ProcessMessageAsync(serverId, data, latencyMs);
                    
                    Array.Copy(buffer, i + delimiterBytes.Length, buffer, 0, bytesRead - i - delimiterBytes.Length);
                    bytesRead = bytesRead - i - delimiterBytes.Length;
                    i = -1;
                }
            }
            
            if (bytesRead > 0)
            {
                var remainingData = new byte[bytesRead];
                Array.Copy(buffer, remainingData, bytesRead);
                await ProcessMessageAsync(serverId, remainingData, latencyMs);
            }
        }
    }

    private async Task ProcessMessageAsync(string connectionId, byte[] data, double latencyMs)
    {
        if (_connectionInfos.TryGetValue(connectionId, out var info))
        {
            info.MessagesReceived++;
        }

        _performanceMonitor.RecordMessageReceived("TCP", data.Length);

        if (_dataHandlers.TryGetValue(connectionId, out var handler))
        {
            handler(data);
        }

        DataReceived?.Invoke(connectionId, data);
        
        await Task.CompletedTask;
    }

    private async Task ConnectClientAsync(string connectionId, TcpConfig config, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (attempt < config.MaxReconnectAttempts || config.AutoReconnect)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var client = new TcpClient();
                
                await client.ConnectAsync(config.Host, config.Port);
                
                stopwatch.Stop();
                _clients[connectionId] = client;

                if (_connectionInfos.TryGetValue(connectionId, out var info))
                {
                    info.Status = TcpConnectionStatus.Connected;
                    info.ConnectedAt = DateTime.UtcNow;
                }

                _performanceMonitor.RecordMessageSent("TCP", 0, stopwatch.Elapsed.TotalMilliseconds);
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

                _performanceMonitor.RecordError("TCP");
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
        var buffer = new byte[config.ReceiveBufferSize > 0 ? config.ReceiveBufferSize : _defaultBufferSize];
        var stream = client.GetStream();
        var stopwatch = new Stopwatch();

        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                stopwatch.Restart();
                var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                stopwatch.Stop();
                
                if (bytesRead == 0) break;

                _performanceMonitor.RecordMessageReceived("TCP", bytesRead);
                
                if (_connectionInfos.TryGetValue(connectionId, out var info))
                {
                    info.BytesReceived += bytesRead;
                    info.LastDataAt = DateTime.UtcNow;
                }

                if (config.UseDelimiter && !string.IsNullOrEmpty(config.Delimiter))
                {
                    await ProcessDelimitedMessagesAsync(connectionId, buffer, bytesRead, config.Delimiter, stopwatch.Elapsed.TotalMilliseconds);
                }
                else
                {
                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    await ProcessMessageAsync(connectionId, data, stopwatch.Elapsed.TotalMilliseconds);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _performanceMonitor.RecordError("TCP");
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
        var stopwatch = Stopwatch.StartNew();
        
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
                            await stream.WriteAsync(data, cancellationToken: CancellationToken.None);
                            await stream.FlushAsync(CancellationToken.None);

                            if (_connectionInfos.TryGetValue(connectionId, out var info))
                            {
                                info.BytesSent += data.Length;
                                info.MessagesSent++;
                            }
                            
                            _performanceMonitor.RecordMessageSent("TCP-Server", data.Length, stopwatch.Elapsed.TotalMilliseconds);
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
                await stream.WriteAsync(data, cancellationToken: CancellationToken.None);
                await stream.FlushAsync(CancellationToken.None);

                if (_connectionInfos.TryGetValue(connectionId, out var info))
                {
                    info.BytesSent += data.Length;
                    info.MessagesSent++;
                }
                
                _performanceMonitor.RecordMessageSent("TCP", data.Length, stopwatch.Elapsed.TotalMilliseconds);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _performanceMonitor.RecordError("TCP");
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
        _performanceMonitor.RecordConnectionClose("TCP", connectionId);
        
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
        _receiveQueues.TryRemove(connectionId, out _);
        _backpressureController.RemoveConnection(connectionId);

        ConnectionStatusChanged?.Invoke(connectionId, TcpConnectionStatus.Disconnected);
    }

    public Task RegisterDataHandlerAsync(string connectionId, Action<byte[]> handler)
    {
        _dataHandlers[connectionId] = handler;
        return Task.CompletedTask;
    }

    public Task<PerformanceMetricsSnapshot> GetPerformanceMetricsAsync()
    {
        return _performanceMonitor.GetCurrentSnapshotAsync();
    }

    public Task<BackpressureState?> GetBackpressureStateAsync(string connectionId)
    {
        return Task.FromResult(_backpressureController.GetState(connectionId));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var cts in _connectionCts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _connectionCts.Clear();

        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();

        foreach (var listener in _servers.Values)
        {
            listener.Stop();
        }
        _servers.Clear();

        foreach (var streams in _serverClients.Values)
        {
            foreach (var stream in streams.Values)
            {
                stream.Dispose();
            }
        }
        _serverClients.Clear();

        _receiveQueues.Clear();
        _dataHandlers.Clear();
    }
}
