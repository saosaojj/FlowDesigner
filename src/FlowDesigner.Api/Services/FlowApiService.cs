using FlowDesigner.Shared.Models;

namespace FlowDesigner.Api.Services;

public class FlowApiService
{
    private readonly FlowService _flowService;
    private readonly NodeRegistryService _nodeRegistryService;
    private readonly DashboardService _dashboardService;

    public FlowApiService(
        FlowService flowService,
        NodeRegistryService nodeRegistryService,
        DashboardService dashboardService)
    {
        _flowService = flowService;
        _nodeRegistryService = nodeRegistryService;
        _dashboardService = dashboardService;
    }

    public async Task<List<Flow>?> GetAllFlowsAsync()
    {
        return await _flowService.GetAllFlowsAsync();
    }

    public async Task<Flow?> GetFlowAsync(string id)
    {
        return await _flowService.GetFlowAsync(id);
    }

    public async Task<Flow?> CreateFlowAsync(Flow flow)
    {
        return await _flowService.CreateFlowAsync(flow);
    }

    public async Task<Flow?> UpdateFlowAsync(string id, Flow flow)
    {
        return await _flowService.UpdateFlowAsync(id, flow);
    }

    public async Task<bool> DeleteFlowAsync(string id)
    {
        return await _flowService.DeleteFlowAsync(id);
    }

    public async Task<List<NodeDefinition>?> GetNodeDefinitionsAsync()
    {
        return await _nodeRegistryService.GetAllNodeDefinitionsAsync();
    }

    public async Task<List<DashboardConfig>?> GetAllDashboardsAsync()
    {
        return await _dashboardService.GetAllDashboardsAsync();
    }

    public async Task<DashboardConfig?> GetDashboardAsync(string id)
    {
        return await _dashboardService.GetDashboardAsync(id);
    }

    public async Task<DashboardDataSnapshot?> GetDashboardDataAsync(string id)
    {
        var result = await _dashboardService.GetDashboardDataAsync(id);
        return result;
    }

    public async Task<DashboardConfig?> CreateDashboardAsync(DashboardConfig config)
    {
        return await _dashboardService.CreateDashboardAsync(config);
    }

    public async Task<DashboardConfig?> UpdateDashboardAsync(string id, DashboardConfig config)
    {
        return await _dashboardService.UpdateDashboardAsync(id, config);
    }

    public async Task<bool> DeleteDashboardAsync(string id)
    {
        return await _dashboardService.DeleteDashboardAsync(id);
    }

    public async Task<List<DashboardTemplate>?> GetDashboardTemplatesAsync()
    {
        return await _dashboardService.GetTemplatesAsync();
    }

    public async Task<List<AlarmRecord>?> GetRecentAlarmsAsync(int count = 20)
    {
        return await _dashboardService.GetRecentAlarmsAsync(count);
    }

    public async Task<List<DeviceStatus>?> GetAllDevicesAsync()
    {
        return await _dashboardService.GetAllDevicesAsync();
    }
}
