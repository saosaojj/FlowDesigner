using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class WebSocketService
{
    private readonly ConcurrentDictionary<string, ClientWebSocket> _connections = new();
    private readonly ConcurrentDictionary<string, WebSocketConnectionInfo> _connectionInfos = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _connectionCts = new();
    private readonly ConcurrentDictionary<string, Action<WebSocketMessage>> _messageHandlers = new();
    
    public event Action<string, WebSocketConnectionStatus>? ConnectionStatusChanged;
    public event Action<string, WebSocketMessage>? MessageReceived;

    public Task<List<WebSocketConnectionInfo>> GetAllConnectionsAsync()
    {
        return Task.FromResult(_connectionInfos.Values.ToList());
    }

    public Task<WebSocketConnectionInfo?> GetConnectionAsync(string connectionId)
    {
        _connectionInfos.TryGetValue(connectionId, out var info);
        return Task.FromResult(info);
    }

    public async Task<string> ConnectAsync(WebSocketConfig config, string connectionName = "")
    {
        var connectionId = Guid.NewGuid().ToString();
        var connectionNameVal = string.IsNullOrEmpty(connectionName) ? $"Connection-{connectionId}" : connectionName;
        
        var connectionInfo = new WebSocketConnectionInfo
        {
            Id = connectionId,
            Name = connectionNameVal,
            Url = config.Url,
            Status = WebSocketConnectionStatus.Connecting
        };
        
        _connectionInfos[connectionId] = connectionInfo;

        var cts = new CancellationTokenSource();
        _connectionCts[connectionId] = cts;

        var client = new ClientWebSocket();
        if (config.Headers != null)
        {
            foreach (var header in config.Headers)
            {
                client.Options.SetRequestHeader(header.Key, header.Value);
            }
        }

        try
        {
            var timeoutCts = new CancellationTokenSource(config.ConnectionTimeout);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token);
            
            await client.ConnectAsync(new Uri(config.Url), linkedCts.Token);
            
            connectionInfo.Status = WebSocketConnectionStatus.Connected;
            connectionInfo.ConnectedAt = DateTime.UtcNow;
            _connections[connectionId] = client;
            
            ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Connected);

            _ = ReceiveLoopAsync(connectionId, client, config, cts.Token);
        }
        catch (Exception ex)
        {
            connectionInfo.Status = WebSocketConnectionStatus.Error;
            connectionInfo.ErrorCount++;
            ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Error);
            
            Console.WriteLine($"WebSocket connection failed: {ex.Message}");
            
            if (config.AutoReconnect)
            {
                _ = ReconnectAsync(connectionId, config);
            }
            
            throw;
        }

        return connectionId;
    }

    private async Task ReceiveLoopAsync(string connectionId, ClientWebSocket client, WebSocketConfig config, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && client.State == WebSocketState.Open)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    break;
                }

                var message = new WebSocketMessage
                {
                    Type = result.MessageType == WebSocketMessageType.Binary ? "binary" : "text",
                    Timestamp = DateTime.UtcNow
                };

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var data = new byte[result.Count];
                    Array.Copy(buffer, data, result.Count);
                    message.Payload = data;
                }
                else
                {
                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        message.Payload = System.Text.Json.JsonSerializer.Deserialize<object>(text);
                        message.Type = "json";
                    }
                    catch
                    {
                        message.Payload = text;
                        message.Type = "text";
                    }
                }

                if (_connectionInfos.TryGetValue(connectionId, out var info))
                {
                    info.MessageReceivedCount++;
                    info.LastMessageAt = DateTime.UtcNow;
                }

                if (_messageHandlers.TryGetValue(connectionId, out var handler))
                {
                    handler(message);
                }

                MessageReceived?.Invoke(connectionId, message);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        catch (Exception ex)
        {
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.ErrorCount++;
            }
            Console.WriteLine($"WebSocket receive error: {ex.Message}");
        }
        finally
        {
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.Status = WebSocketConnectionStatus.Disconnected;
            }
            _connections.TryRemove(connectionId, out _);
            ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Disconnected);
            
            if (config.AutoReconnect)
            {
                _ = ReconnectAsync(connectionId, config);
            }
        }
    }

    private async Task ReconnectAsync(string connectionId, WebSocketConfig config)
    {
        if (!_connectionInfos.TryGetValue(connectionId, out var info))
            return;
        
        info.Status = WebSocketConnectionStatus.Reconnecting;
        ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Reconnecting);

        for (var attempt = 1; attempt <= config.MaxReconnectAttempts; attempt++)
        {
            await Task.Delay(config.ReconnectInterval * attempt);
            
            try
            {
                var oldCts = _connectionCts.GetValueOrDefault(connectionId);
                if (oldCts != null)
                {
                    oldCts.Cancel();
                    oldCts.Dispose();
                }

                var newCts = new CancellationTokenSource();
                _connectionCts[connectionId] = newCts;

                var client = new ClientWebSocket();
                if (config.Headers != null)
                {
                    foreach (var header in config.Headers)
                    {
                        client.Options.SetRequestHeader(header.Key, header.Value);
                    }
                }

                await client.ConnectAsync(new Uri(config.Url), newCts.Token);
                
                info.Status = WebSocketConnectionStatus.Connected;
                info.ConnectedAt = DateTime.UtcNow;
                _connections[connectionId] = client;
                
                ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Connected);
                
                _ = ReceiveLoopAsync(connectionId, client, config, newCts.Token);
                
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reconnect attempt {attempt} failed: {ex.Message}");
                
                if (attempt == config.MaxReconnectAttempts)
                {
                    info.Status = WebSocketConnectionStatus.Error;
                    ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Error);
                }
            }
        }
    }

    public async Task<bool> SendAsync(string connectionId, WebSocketMessage message)
    {
        if (!_connections.TryGetValue(connectionId, out var client) || client.State != WebSocketState.Open)
            return false;

        try
        {
            byte[] data;
            WebSocketMessageType messageType;

            if (message.Type == "binary" && message.Payload is byte[] binaryData)
            {
                data = binaryData;
                messageType = WebSocketMessageType.Binary;
            }
            else if (message.Type == "json")
            {
                data = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(message.Payload));
                messageType = WebSocketMessageType.Text;
            }
            else
            {
                data = Encoding.UTF8.GetBytes(message.Payload?.ToString() ?? "");
                messageType = WebSocketMessageType.Text;
            }

            await client.SendAsync(new ArraySegment<byte>(data), messageType, true, CancellationToken.None);
            
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.MessageSentCount++;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket send error: {ex.Message}");
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.ErrorCount++;
            }
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

        if (_connections.TryGetValue(connectionId, out var client))
        {
            try
            {
                if (client.State == WebSocketState.Open)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected by user", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing WebSocket: {ex.Message}");
            }
            finally
            {
                client.Dispose();
                _connections.TryRemove(connectionId, out _);
            }
        }

        if (_connectionInfos.TryGetValue(connectionId, out var info))
        {
            info.Status = WebSocketConnectionStatus.Disconnected;
        }
        _messageHandlers.TryRemove(connectionId, out _);
        
        ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Disconnected);
    }

    public Task RegisterMessageHandlerAsync(string connectionId, Action<WebSocketMessage> handler)
    {
        _messageHandlers[connectionId] = handler;
        return Task.CompletedTask;
    }

    public Task UnregisterMessageHandlerAsync(string connectionId)
    {
        _messageHandlers.TryRemove(connectionId, out _);
        return Task.CompletedTask;
    }
}
