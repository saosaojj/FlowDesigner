using FlowDesigner.Api.Services;
using FlowDesigner.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesigner.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardsController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public DashboardsController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DashboardConfig>>> GetAll()
    {
        return await _dashboardService.GetAllDashboardsAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DashboardConfig>> Get(string id)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(id);
        if (dashboard == null)
            return NotFound();
        return dashboard;
    }

    [HttpGet("{id}/data")]
    public async Task<ActionResult<DashboardDataSnapshot>> GetData(string id)
    {
        return await _dashboardService.GetDashboardDataAsync(id);
    }

    [HttpPost]
    public async Task<ActionResult<DashboardConfig>> Create([FromBody] DashboardConfig config)
    {
        var created = await _dashboardService.CreateDashboardAsync(config);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DashboardConfig>> Update(string id, [FromBody] DashboardConfig config)
    {
        var updated = await _dashboardService.UpdateDashboardAsync(id, config);
        if (updated == null)
            return NotFound();
        return updated;
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var success = await _dashboardService.DeleteDashboardAsync(id);
        if (!success)
            return NotFound();
        return NoContent();
    }

    [HttpGet("templates/list")]
    public async Task<ActionResult<List<DashboardTemplate>>> GetTemplates()
    {
        return await _dashboardService.GetTemplatesAsync();
    }

    [HttpGet("alarms/recent")]
    public async Task<ActionResult<List<AlarmRecord>>> GetRecentAlarms([FromQuery] int count = 20)
    {
        return await _dashboardService.GetRecentAlarmsAsync(count);
    }

    [HttpPost("alarms")]
    public async Task<ActionResult> AddAlarm([FromBody] AlarmRecord alarm)
    {
        await _dashboardService.AddAlarmAsync(alarm);
        return Ok();
    }

    [HttpPost("alarms/{alarmId}/acknowledge")]
    public async Task<ActionResult> AcknowledgeAlarm(string alarmId, [FromQuery] string userId = "system")
    {
        await _dashboardService.AcknowledgeAlarmAsync(alarmId, userId);
        return Ok();
    }

    [HttpGet("devices/list")]
    public async Task<ActionResult<List<DeviceStatus>>> GetDevices()
    {
        return await _dashboardService.GetAllDevicesAsync();
    }

    [HttpPut("devices/{deviceId}")]
    public async Task<ActionResult> UpdateDeviceStatus(string deviceId, [FromBody] DeviceStatus device)
    {
        device.Id = deviceId;
        await _dashboardService.UpdateDeviceStatusAsync(device);
        return Ok();
    }
}

[ApiController]
[Route("api/[controller]")]
public class NodesController : ControllerBase
{
    private readonly NodeRegistryService _nodeRegistry;

    public NodesController(NodeRegistryService nodeRegistry)
    {
        _nodeRegistry = nodeRegistry;
    }

    [HttpGet("definitions")]
    public async Task<ActionResult<List<NodeDefinition>>> GetDefinitions()
    {
        return await _nodeRegistry.GetAllNodeDefinitionsAsync();
    }

    [HttpGet("definitions/{type}")]
    public async Task<ActionResult<NodeDefinition>> GetDefinition(string type)
    {
        var definition = await _nodeRegistry.GetNodeDefinitionAsync(type);
        if (definition == null)
            return NotFound();
        return definition;
    }
}

[ApiController]
[Route("api/[controller]")]
public class WebSocketController : ControllerBase
{
    private readonly WebSocketService _webSocketService;

    public WebSocketController(WebSocketService webSocketService)
    {
        _webSocketService = webSocketService;
    }

    [HttpGet("connections")]
    public async Task<ActionResult<List<WebSocketConnectionInfo>>> GetConnections()
    {
        return await _webSocketService.GetAllConnectionsAsync();
    }

    [HttpGet("connections/{id}")]
    public async Task<ActionResult<WebSocketConnectionInfo>> GetConnection(string id)
    {
        var connection = await _webSocketService.GetConnectionAsync(id);
        if (connection == null)
            return NotFound();
        return connection;
    }

    [HttpPost("connect")]
    public async Task<ActionResult<string>> Connect([FromBody] WebSocketConfig config, [FromQuery] string? name)
    {
        var connectionId = await _webSocketService.ConnectAsync(config, name ?? "");
        return Ok(new { connectionId });
    }

    [HttpPost("connections/{id}/disconnect")]
    public async Task<ActionResult> Disconnect(string id)
    {
        await _webSocketService.DisconnectAsync(id);
        return Ok();
    }

    [HttpPost("connections/{id}/send")]
    public async Task<ActionResult> SendMessage(string id, [FromBody] WebSocketMessage message)
    {
        var success = await _webSocketService.SendAsync(id, message);
        if (!success)
            return BadRequest("Failed to send message");
        return Ok();
    }
}

[ApiController]
[Route("api/[controller]")]
public class TcpController : ControllerBase
{
    private readonly TcpService _tcpService;

    public TcpController(TcpService tcpService)
    {
        _tcpService = tcpService;
    }

    [HttpGet("connections")]
    public async Task<ActionResult<List<TcpConnectionInfo>>> GetConnections()
    {
        return await _tcpService.GetAllConnectionsAsync();
    }

    [HttpGet("connections/{id}")]
    public async Task<ActionResult<TcpConnectionInfo>> GetConnection(string id)
    {
        var connection = await _tcpService.GetConnectionAsync(id);
        if (connection == null)
            return NotFound();
        return connection;
    }

    [HttpPost("connect")]
    public async Task<ActionResult<string>> Connect([FromBody] TcpConfig config, [FromQuery] string? name)
    {
        var connectionId = await _tcpService.ConnectAsync(config, name ?? "");
        return Ok(new { connectionId });
    }

    [HttpPost("connections/{id}/disconnect")]
    public async Task<ActionResult> Disconnect(string id)
    {
        await _tcpService.DisconnectAsync(id);
        return Ok();
    }

    [HttpPost("connections/{id}/send")]
    public async Task<ActionResult> Send(string id, [FromBody] byte[] data)
    {
        var success = await _tcpService.SendAsync(id, data);
        if (!success)
            return BadRequest("Failed to send data");
        return Ok();
    }

    [HttpPost("connections/{id}/sendString")]
    public async Task<ActionResult> SendString(string id, [FromBody] string message)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(message);
        var success = await _tcpService.SendAsync(id, data);
        if (!success)
            return BadRequest("Failed to send message");
        return Ok();
    }
}

[ApiController]
[Route("api/[controller]")]
public class RtpController : ControllerBase
{
    private readonly RtpService _rtpService;

    public RtpController(RtpService rtpService)
    {
        _rtpService = rtpService;
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<RtpSessionInfo>>> GetSessions()
    {
        return await _rtpService.GetAllSessionsAsync();
    }

    [HttpGet("sessions/{id}")]
    public async Task<ActionResult<RtpSessionInfo>> GetSession(string id)
    {
        var session = await _rtpService.GetSessionAsync(id);
        if (session == null)
            return NotFound();
        return session;
    }

    [HttpPost("start")]
    public async Task<ActionResult<string>> StartSession([FromBody] RtpConfig config, [FromQuery] string? name)
    {
        var sessionId = await _rtpService.StartSessionAsync(config, name ?? "");
        return Ok(new { sessionId });
    }

    [HttpPost("sessions/{id}/stop")]
    public async Task<ActionResult> StopSession(string id)
    {
        await _rtpService.StopSessionAsync(id);
        return Ok();
    }

    [HttpPost("sessions/{id}/send")]
    public async Task<ActionResult> SendPacket(string id, [FromBody] byte[] data, [FromQuery] bool marker = false)
    {
        var success = await _rtpService.SendPacketAsync(id, data, marker);
        if (!success)
            return BadRequest("Failed to send packet");
        return Ok();
    }
}
