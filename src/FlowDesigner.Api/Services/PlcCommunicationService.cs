using FlowDesigner.Shared.Models;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace FlowDesigner.Api.Services;

public class PlcCommunicationService
{
    private readonly ILogger<PlcCommunicationService> _logger;
    private readonly ConcurrentDictionary<string, TcpClient> _modbusConnections = new();
    private readonly ConcurrentDictionary<string, TcpClient> _s7Connections = new();
    private readonly ConcurrentDictionary<string, PlcConnectionState> _connectionStates = new();

    public PlcCommunicationService(ILogger<PlcCommunicationService> logger)
    {
        _logger = logger;
    }

    public async Task<PlcConnectionState> ConnectAsync(PlcConnection connection)
    {
        try
        {
            _logger.LogInformation("正在连接 PLC: {Name} ({Protocol})", 
                connection.Name, connection.Protocol);

            var state = new PlcConnectionState
            {
                Connection = connection,
                IsConnected = false,
                LastError = null,
                ConnectedAt = null
            };

            switch (connection.Protocol)
            {
                case PlcProtocol.ModbusTcp:
                    var modbusResult = await ConnectModbusAsync(connection);
                    if (modbusResult)
                    {
                        state.IsConnected = true;
                        state.ConnectedAt = DateTime.UtcNow;
                    }
                    break;

                case PlcProtocol.S7:
                    var s7Result = await ConnectS7Async(connection);
                    if (s7Result)
                    {
                        state.IsConnected = true;
                        state.ConnectedAt = DateTime.UtcNow;
                    }
                    break;
            }

            _connectionStates[connection.Id] = state;
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC 连接失败: {Name}", connection.Name);
            return new PlcConnectionState
            {
                Connection = connection,
                IsConnected = false,
                LastError = ex.Message
            };
        }
    }

    public async Task DisconnectAsync(string connectionId)
    {
        _logger.LogInformation("断开 PLC 连接: {ConnectionId}", connectionId);

        if (_modbusConnections.TryRemove(connectionId, out var modbusClient))
        {
            modbusClient.Close();
            modbusClient.Dispose();
        }

        if (_s7Connections.TryRemove(connectionId, out var s7Client))
        {
            s7Client.Close();
            s7Client.Dispose();
        }

        if (_connectionStates.TryGetValue(connectionId, out var state))
        {
            state.IsConnected = false;
            state.ConnectedAt = null;
        }
    }

    public async Task<PlcReadResult> ReadAsync(PlcReadRequest request)
    {
        try
        {
            var state = GetConnectionState(request.ConnectionId);
            if (state == null || !state.IsConnected)
            {
                return new PlcReadResult
                {
                    Success = false,
                    Error = "PLC 未连接"
                };
            }

            switch (state.Connection.Protocol)
            {
                case PlcProtocol.ModbusTcp:
                    return await ReadModbusAsync(state.Connection, request);
                case PlcProtocol.S7:
                    return await ReadS7Async(state.Connection, request);
                default:
                    return new PlcReadResult
                    {
                        Success = false,
                        Error = "不支持的协议"
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC 读取失败");
            return new PlcReadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<PlcReadResult> WriteAsync(PlcWriteRequest request)
    {
        try
        {
            var state = GetConnectionState(request.ConnectionId);
            if (state == null || !state.IsConnected)
            {
                return new PlcReadResult
                {
                    Success = false,
                    Error = "PLC 未连接"
                };
            }

            switch (state.Connection.Protocol)
            {
                case PlcProtocol.ModbusTcp:
                    return await WriteModbusAsync(state.Connection, request);
                case PlcProtocol.S7:
                    return await WriteS7Async(state.Connection, request);
                default:
                    return new PlcReadResult
                    {
                        Success = false,
                        Error = "不支持的协议"
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PLC 写入失败");
            return new PlcReadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private PlcConnectionState? GetConnectionState(string connectionId)
    {
        return _connectionStates.GetValueOrDefault(connectionId);
    }

    private async Task<bool> ConnectModbusAsync(PlcConnection connection)
    {
        try
        {
            var client = new TcpClient();
            await client.ConnectAsync(connection.IpAddress, connection.Port);
            _modbusConnections[connection.Id] = client;
            _logger.LogInformation("Modbus TCP 连接成功: {IpAddress}:{Port}", 
                connection.IpAddress, connection.Port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Modbus TCP 连接失败");
            return false;
        }
    }

    private async Task<bool> ConnectS7Async(PlcConnection connection)
    {
        try
        {
            var client = new TcpClient();
            await client.ConnectAsync(connection.IpAddress, connection.Port);
            _s7Connections[connection.Id] = client;
            _logger.LogInformation("S7 连接成功: {IpAddress}:{Port}", 
                connection.IpAddress, connection.Port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "S7 连接失败");
            return false;
        }
    }

    private async Task<PlcReadResult> ReadModbusAsync(PlcConnection connection, PlcReadRequest request)
    {
        if (!_modbusConnections.TryGetValue(connection.Id, out var client) || !client.Connected)
        {
            return new PlcReadResult { Success = false, Error = "Modbus 连接已断开" };
        }

        var functionCode = GetModbusFunctionCode(request.DataType);
        var address = ParseModbusAddress(request.Address);
        
        var requestBytes = BuildModbusReadRequest(
            (byte)(connection.Properties.GetValueOrDefault("slaveId") ?? 1),
            (byte)functionCode,
            (ushort)address,
            (ushort)request.Count
        );

        var stream = client.GetStream();
        await stream.WriteAsync(requestBytes);
        
        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        
        if (bytesRead > 0)
        {
            var data = ExtractModbusData(response, bytesRead);
            return new PlcReadResult
            {
                Success = true,
                Value = data,
                Timestamp = DateTime.UtcNow
            };
        }

        return new PlcReadResult { Success = false, Error = "读取超时" };
    }

    private async Task<PlcReadResult> WriteModbusAsync(PlcConnection connection, PlcWriteRequest request)
    {
        if (!_modbusConnections.TryGetValue(connection.Id, out var client) || !client.Connected)
        {
            return new PlcReadResult { Success = false, Error = "Modbus 连接已断开" };
        }

        var functionCode = GetModbusWriteFunctionCode(request.DataType);
        var address = ParseModbusAddress(request.Address);
        
        var requestBytes = BuildModbusWriteRequest(
            (byte)(connection.Properties.GetValueOrDefault("slaveId") ?? 1),
            (byte)functionCode,
            (ushort)address,
            request.Value
        );

        var stream = client.GetStream();
        await stream.WriteAsync(requestBytes);
        
        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        
        if (bytesRead > 0 && response[7] == functionCode)
        {
            return new PlcReadResult
            {
                Success = true,
                Value = request.Value,
                Timestamp = DateTime.UtcNow
            };
        }

        return new PlcReadResult { Success = false, Error = "写入失败" };
    }

    private async Task<PlcReadResult> ReadS7Async(PlcConnection connection, PlcReadRequest request)
    {
        if (!_s7Connections.TryGetValue(connection.Id, out var client) || !client.Connected)
        {
            return new PlcReadResult { Success = false, Error = "S7 连接已断开" };
        }

        var address = ParseS7Address(request.Address);
        
        var isoPacket = BuildS7ReadRequest((byte)connection.Rack, (byte)connection.Slot, address);
        var stream = client.GetStream();
        await stream.WriteAsync(isoPacket);
        
        var response = new byte[2048];
        var bytesRead = await stream.ReadAsync(response);
        
        if (bytesRead > 0)
        {
            var data = ExtractS7Data(response, bytesRead);
            return new PlcReadResult
            {
                Success = true,
                Value = data,
                Timestamp = DateTime.UtcNow
            };
        }

        return new PlcReadResult { Success = false, Error = "读取超时" };
    }

    private async Task<PlcReadResult> WriteS7Async(PlcConnection connection, PlcWriteRequest request)
    {
        if (!_s7Connections.TryGetValue(connection.Id, out var client) || !client.Connected)
        {
            return new PlcReadResult { Success = false, Error = "S7 连接已断开" };
        }

        var address = ParseS7Address(request.Address);
        var isoPacket = BuildS7WriteRequest((byte)connection.Rack, (byte)connection.Slot, address, request.Value);
        var stream = client.GetStream();
        await stream.WriteAsync(isoPacket);
        
        var response = new byte[1024];
        var bytesRead = await stream.ReadAsync(response);
        
        if (bytesRead > 0 && response[17] == 0x04)
        {
            return new PlcReadResult
            {
                Success = true,
                Value = request.Value,
                Timestamp = DateTime.UtcNow
            };
        }

        return new PlcReadResult { Success = false, Error = "写入失败" };
    }

    private int GetModbusFunctionCode(PlcDataType dataType)
    {
        return dataType switch
        {
            PlcDataType.Bit => 0x01,
            PlcDataType.Byte => 0x03,
            PlcDataType.Word => 0x03,
            PlcDataType.DWord => 0x03,
            PlcDataType.Int => 0x04,
            _ => 0x03
        };
    }

    private int GetModbusWriteFunctionCode(PlcDataType dataType)
    {
        return dataType switch
        {
            PlcDataType.Bit => 0x05,
            PlcDataType.Word => 0x06,
            PlcDataType.DWord => 0x10,
            _ => 0x06
        };
    }

    private int ParseModbusAddress(string address)
    {
        if (int.TryParse(address, out var result))
            return result;
        return 0;
    }

    private (int db, int byteOffset, int bit) ParseS7Address(string address)
    {
        var parts = address.Split('.');
        if (parts.Length >= 2)
        {
            var db = parts[0].Replace("DB", "").Trim();
            var byteOffset = int.TryParse(parts[1], out var b) ? b : 0;
            var bit = parts.Length > 2 && int.TryParse(parts[2], out var bitVal) ? bitVal : 0;
            return (int.TryParse(db, out var dbNum) ? dbNum : 1, byteOffset, bit);
        }
        return (1, 0, 0);
    }

    private byte[] BuildModbusReadRequest(byte unitId, byte functionCode, ushort startAddress, ushort quantity)
    {
        var request = new byte[12];
        request[0] = (byte)(0x1234 >> 8);
        request[1] = (byte)(0x1234 & 0xFF);
        request[2] = 0x00;
        request[3] = 0x00;
        request[4] = 0x00;
        request[5] = 0x06;
        request[6] = unitId;
        request[7] = functionCode;
        request[8] = (byte)(startAddress >> 8);
        request[9] = (byte)(startAddress & 0xFF);
        request[10] = (byte)(quantity >> 8);
        request[11] = (byte)(quantity & 0xFF);
        return request;
    }

    private byte[] BuildModbusWriteRequest(byte unitId, byte functionCode, ushort startAddress, object? value)
    {
        var request = new byte[15];
        request[0] = 0x12;
        request[1] = 0x34;
        request[2] = 0x00;
        request[3] = 0x00;
        request[4] = 0x00;
        request[5] = 0x06;
        request[6] = unitId;
        request[7] = functionCode;
        request[8] = (byte)(startAddress >> 8);
        request[9] = (byte)(startAddress & 0xFF);
        request[10] = (byte)((value as int? ?? 0) >> 8);
        request[11] = (byte)((value as int? ?? 0) & 0xFF);
        return request;
    }

    private byte[] BuildS7ReadRequest(byte rack, byte slot, (int db, int byteOffset, int bit) address)
    {
        return new byte[0];
    }

    private byte[] BuildS7WriteRequest(byte rack, byte slot, (int db, int byteOffset, int bit) address, object? value)
    {
        return new byte[0];
    }

    private object? ExtractModbusData(byte[] response, int length)
    {
        if (length > 8)
        {
            var byteCount = response[8];
            var data = new byte[byteCount];
            Array.Copy(response, 9, data, 0, byteCount);
            return BitConverter.ToUInt16(data, 0);
        }
        return null;
    }

    private object? ExtractS7Data(byte[] response, int length)
    {
        if (length > 27)
        {
            var dataLength = (response[25] << 8) | response[26];
            if (length >= 27 + dataLength)
            {
                var data = new byte[dataLength];
                Array.Copy(response, 27, data, 0, dataLength);
                return BitConverter.ToUInt32(data, 0);
            }
        }
        return null;
    }
}

public class PlcConnectionState
{
    public PlcConnection Connection { get; set; } = null!;
    public bool IsConnected { get; set; }
    public string? LastError { get; set; }
    public DateTime? ConnectedAt { get; set; }
}
