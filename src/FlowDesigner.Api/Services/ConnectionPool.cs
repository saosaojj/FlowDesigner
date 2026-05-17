using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace FlowDesigner.Api.Services;

public class ConnectionPool<T> where T : class
{
    private readonly ConcurrentBag<T> _availableConnections = new();
    private readonly ConcurrentDictionary<string, T> _activeConnections = new();
    private readonly Func<string, T> _connectionFactory;
    private readonly Action<T>? _cleanupAction;
    private readonly int _maxPoolSize;
    private readonly int _minPoolSize;
    private readonly TimeSpan _connectionIdleTimeout;
    private readonly Timer _cleanupTimer;
    
    private int _totalCreated;
    private int _totalDisposed;
    
    public ConnectionPool(
        Func<string, T> connectionFactory,
        Action<T>? cleanupAction = null,
        int maxPoolSize = 100,
        int minPoolSize = 5,
        TimeSpan? connectionIdleTimeout = null)
    {
        _connectionFactory = connectionFactory;
        _cleanupAction = cleanupAction;
        _maxPoolSize = maxPoolSize;
        _minPoolSize = minPoolSize;
        _connectionIdleTimeout = connectionIdleTimeout ?? TimeSpan.FromMinutes(5);
        
        _cleanupTimer = new Timer(
            CleanupIdleConnections,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

    public async Task<T> GetConnectionAsync(string connectionKey)
    {
        if (_activeConnections.ContainsKey(connectionKey))
        {
            return _activeConnections[connectionKey];
        }
        
        if (_availableConnections.TryTake(out var connection))
        {
            _activeConnections[connectionKey] = connection;
            return connection;
        }
        
        if (Interlocked.Increment(ref _totalCreated) <= _maxPoolSize || _availableConnections.IsEmpty)
        {
            var newConnection = _connectionFactory(connectionKey);
            _activeConnections[connectionKey] = newConnection;
            return newConnection;
        }
        
        Interlocked.Decrement(ref _totalCreated);
        
        var timeout = TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;
        
        while (DateTime.UtcNow - startTime < timeout)
        {
            await Task.Delay(100);
            
            if (_availableConnections.TryTake(out var reusedConnection))
            {
                _activeConnections[connectionKey] = reusedConnection;
                return reusedConnection;
            }
        }
        
        throw new InvalidOperationException($"连接池已满，无法获取连接: {connectionKey}");
    }

    public void ReturnConnection(string connectionKey)
    {
        if (_activeConnections.TryRemove(connectionKey, out var connection))
        {
            _availableConnections.Add(connection);
        }
    }

    public void RemoveConnection(string connectionKey)
    {
        if (_activeConnections.TryRemove(connectionKey, out var connection))
        {
            _cleanupAction?.Invoke(connection);
            Interlocked.Increment(ref _totalDisposed);
        }
    }

    private void CleanupIdleConnections(object? state)
    {
        while (_availableConnections.Count > _minPoolSize && _availableConnections.TryTake(out var connection))
        {
            _cleanupAction?.Invoke(connection);
            Interlocked.Increment(ref _totalDisposed);
        }
    }

    public int AvailableCount => _availableConnections.Count;
    public int ActiveCount => _activeConnections.Count;
    public int TotalCreated => _totalCreated;
    public int TotalDisposed => _totalDisposed;

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        
        foreach (var connection in _availableConnections)
        {
            _cleanupAction?.Invoke(connection);
        }
        _availableConnections.Clear();
        
        foreach (var connection in _activeConnections.Values)
        {
            _cleanupAction?.Invoke(connection);
        }
        _activeConnections.Clear();
    }
}

public class WebSocketConnectionPool
{
    private readonly ConnectionPool<ClientWebSocket> _pool;
    private readonly ConcurrentDictionary<string, ClientWebSocket> _activeConnections = new();

    public WebSocketConnectionPool(int maxPoolSize = 50)
    {
        _pool = new ConnectionPool<ClientWebSocket>(
            _ => new ClientWebSocket(),
            ws => 
            {
                try 
                { 
                    ws.Dispose(); 
                }
                catch { }
            },
            maxPoolSize
        );
    }

    public Task<ClientWebSocket> GetConnectionAsync(string url)
    {
        return _pool.GetConnectionAsync(url);
    }

    public void ReturnConnection(string url)
    {
        _pool.ReturnConnection(url);
    }

    public void RemoveConnection(string url)
    {
        _pool.RemoveConnection(url);
    }

    public int AvailableCount => _pool.AvailableCount;
    public int ActiveCount => _pool.ActiveCount;
}

public class TcpConnectionPool
{
    private readonly ConnectionPool<System.Net.Sockets.TcpClient> _pool;

    public TcpConnectionPool(int maxPoolSize = 50)
    {
        _pool = new ConnectionPool<System.Net.Sockets.TcpClient>(
            _ => new System.Net.Sockets.TcpClient(),
            client => 
            {
                try 
                { 
                    client.Close();
                    client.Dispose(); 
                }
                catch { }
            },
            maxPoolSize
        );
    }

    public async Task<(System.Net.Sockets.TcpClient client, System.Net.Sockets.NetworkStream stream)> GetConnectionAsync(
        string host, int port)
    {
        var key = $"{host}:{port}";
        var client = await _pool.GetConnectionAsync(key);
        
        if (!client.Connected)
        {
            await client.ConnectAsync(host, port);
        }
        
        return (client, client.GetStream());
    }

    public void ReturnConnection(string host, int port)
    {
        var key = $"{host}:{port}";
        _pool.ReturnConnection(key);
    }

    public void RemoveConnection(string host, int port)
    {
        var key = $"{host}:{port}";
        _pool.RemoveConnection(key);
    }

    public int AvailableCount => _pool.AvailableCount;
    public int ActiveCount => _pool.ActiveCount;
}
