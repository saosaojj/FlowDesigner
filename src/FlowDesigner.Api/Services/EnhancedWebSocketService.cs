using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class EnhancedWebSocketService : IDisposable
{
    private readonly ConcurrentDictionary<string, ClientWebSocket> _connections = new();
    private readonly ConcurrentDictionary<string, WebSocketConnectionInfo> _connectionInfos = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _connectionCts = new();
    private readonly ConcurrentDictionary<string, Action<WebSocketMessage>> _messageHandlers = new();
    private readonly ConcurrentDictionary<string, Channel<WebSocketMessage>> _receiveQueues = new();
    
    private readonly WebSocketConnectionPool _connectionPool;
    private readonly BackpressureController _backpressureController;
    private readonly CommunicationPerformanceMonitor _performanceMonitor;
    
    private readonly int _defaultBufferSize;
    private bool _disposed;

    public EnhancedWebSocketService(
        WebSocketConnectionPool? connectionPool = null,
        BackpressureController? backpressureController = null,
        CommunicationPerformanceMonitor? performanceMonitor = null)
    {
        _connectionPool = connectionPool ?? new WebSocketConnectionPool(50);
        _backpressureController = backpressureController ?? new BackpressureController();
        _performanceMonitor = performanceMonitor ?? new CommunicationPerformanceMonitor();
        
        _defaultBufferSize = 4096;
    }

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
        _performanceMonitor.RecordConnectionOpen("WebSocket", connectionId);

        var cts = new CancellationTokenSource();
        _connectionCts[connectionId] = cts;
        
        var queue = _backpressureController.GetOrCreateQueue(connectionId);
        _receiveQueues[connectionId] = queue;

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
            var stopwatch = Stopwatch.StartNew();
            var timeoutCts = new CancellationTokenSource(config.ConnectionTimeout);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, timeoutCts.Token);
            
            await client.ConnectAsync(new Uri(config.Url), linkedCts.Token);
            
            stopwatch.Stop();
            
            connectionInfo.Status = WebSocketConnectionStatus.Connected;
            connectionInfo.ConnectedAt = DateTime.UtcNow;
            _connections[connectionId] = client;
            
            _performanceMonitor.RecordMessageSent("WebSocket", 0, stopwatch.Elapsed.TotalMilliseconds);
            ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Connected);

            _ = ReceiveLoopAsync(connectionId, client, config, cts.Token);
        }
        catch (Exception ex)
        {
            connectionInfo.Status = WebSocketConnectionStatus.Error;
            connectionInfo.ErrorCount++;
            _performanceMonitor.RecordError("WebSocket");
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
        var buffer = new byte[_defaultBufferSize * 4];
        var messageBuffer = new byte[_defaultBufferSize * 10];
        var messageLength = 0;
        var stopwatch = new Stopwatch();

        try
        {
            while (!cancellationToken.IsCancellationRequested && client.State == WebSocketState.Open)
            {
                stopwatch.Restart();
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                stopwatch.Stop();
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    break;
                }

                if (result.EndOfMessage)
                {
                    var data = new byte[result.Count];
                    Array.Copy(buffer, data, result.Count);
                    
                    var message = CreateMessage(result.MessageType, data);
                    
                    _performanceMonitor.RecordMessageReceived("WebSocket", data.Length);
                    
                    await ProcessMessageAsync(connectionId, message, stopwatch.Elapsed.TotalMilliseconds);
                }
                else
                {
                    Array.Copy(buffer, 0, messageBuffer, messageLength, result.Count);
                    messageLength += result.Count;
                    
                    if (result.Count == 0 || messageLength >= messageBuffer.Length)
                    {
                        messageLength = 0;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _performanceMonitor.RecordError("WebSocket");
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

    private WebSocketMessage CreateMessage(WebSocketMessageType messageType, byte[] data)
    {
        var message = new WebSocketMessage
        {
            Type = messageType == WebSocketMessageType.Binary ? "binary" : "text",
            Timestamp = DateTime.UtcNow
        };

        if (messageType == WebSocketMessageType.Binary)
        {
            message.Payload = data;
        }
        else
        {
            var text = Encoding.UTF8.GetString(data);
            try
            {
                message.Payload = System.Text.Json.JsonSerializer.Deserialize<object>(text);
                message.Type = "json";
            }
            catch
            {
                message.Payload = text;
            }
        }

        return message;
    }

    private async Task ProcessMessageAsync(string connectionId, WebSocketMessage message, double latencyMs)
    {
        if (_connectionInfos.TryGetValue(connectionId, out var info))
        {
            info.MessageReceivedCount++;
            info.LastMessageAt = DateTime.UtcNow;
        }

        if (_messageHandlers.TryGetValue(connectionId, out var handler))
        {
            handler(message);
        }

        if (_receiveQueues.TryGetValue(connectionId, out var queue))
        {
            await queue.Writer.WriteAsync(message);
        }

        MessageReceived?.Invoke(connectionId, message);
    }

    private async Task ReconnectAsync(string connectionId, WebSocketConfig config)
    {
        if (!_connectionInfos.TryGetValue(connectionId, out var info))
            return;
        
        info.Status = WebSocketConnectionStatus.Reconnecting;
        ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Reconnecting);

        for (var attempt = 1; attempt <= config.MaxReconnectAttempts; attempt++)
        {
            await Task.Delay(config.ReconnectInterval * Math.Min(attempt, 5));
            
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

                var stopwatch = Stopwatch.StartNew();
                await client.ConnectAsync(new Uri(config.Url), newCts.Token);
                stopwatch.Stop();
                
                _performanceMonitor.RecordMessageSent("WebSocket", 0, stopwatch.Elapsed.TotalMilliseconds);
                
                info.Status = WebSocketConnectionStatus.Connected;
                info.ConnectedAt = DateTime.UtcNow;
                _connections[connectionId] = client;
                
                ConnectionStatusChanged?.Invoke(connectionId, WebSocketConnectionStatus.Connected);
                
                _ = ReceiveLoopAsync(connectionId, client, config, newCts.Token);
                
                return;
            }
            catch (Exception ex)
            {
                _performanceMonitor.RecordError("WebSocket");
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

        var stopwatch = Stopwatch.StartNew();

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
            
            stopwatch.Stop();
            
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.MessageSentCount++;
            }
            
            _performanceMonitor.RecordMessageSent("WebSocket", data.Length, stopwatch.Elapsed.TotalMilliseconds);

            return true;
        }
        catch (Exception ex)
        {
            _performanceMonitor.RecordError("WebSocket");
            if (_connectionInfos.TryGetValue(connectionId, out var info))
            {
                info.ErrorCount++;
            }
            Console.WriteLine($"WebSocket send error: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectAsync(string connectionId)
    {
        _performanceMonitor.RecordConnectionClose("WebSocket", connectionId);
        
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
        _receiveQueues.TryRemove(connectionId, out _);
        _backpressureController.RemoveConnection(connectionId);
        
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

    public async Task<WebSocketMessage?> ReadMessageAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (_receiveQueues.TryGetValue(connectionId, out var queue))
        {
            try
            {
                return await queue.Reader.ReadAsync(cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return null;
            }
        }
        return null;
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

        foreach (var client in _connections.Values)
        {
            try
            {
                client.Dispose();
            }
            catch { }
        }
        _connections.Clear();

        _messageHandlers.Clear();
        _receiveQueues.Clear();
    }
}
