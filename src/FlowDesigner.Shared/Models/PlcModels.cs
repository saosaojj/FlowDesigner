namespace FlowDesigner.Shared.Models;

public enum PlcProtocol
{
    ModbusTcp,
    S7
}

public enum PlcDataType
{
    Bit,
    Byte,
    Word,
    DWord,
    Int,
    DInt,
    Real,
    String
}

public class PlcConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public PlcProtocol Protocol { get; set; }
    public string IpAddress { get; set; } = "192.168.1.1";
    public int Port { get; set; } = 502;
    public int Rack { get; set; } = 0;
    public int Slot { get; set; } = 1;
    public int Timeout { get; set; } = 5000;
    public bool IsConnected { get; set; }
}

public class PlcReadRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public PlcDataType DataType { get; set; }
    public string Address { get; set; } = string.Empty;
    public int Count { get; set; } = 1;
}

public class PlcWriteRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public PlcDataType DataType { get; set; }
    public string Address { get; set; } = string.Empty;
    public object? Value { get; set; }
}

public class PlcReadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public object? Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
