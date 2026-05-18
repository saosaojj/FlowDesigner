using FlowDesigner.Shared.Models;
using System.Net.Http.Json;

namespace FlowDesigner.Api.Services;

public class FlowApiService
{
    private readonly HttpClient _httpClient;

    public FlowApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Flow>?> GetAllFlowsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<Flow>>("api/flows");
    }

    public async Task<Flow?> GetFlowAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<Flow>($"api/flows/{id}");
    }

    public async Task<Flow?> CreateFlowAsync(Flow flow)
    {
        var response = await _httpClient.PostAsJsonAsync("api/flows", flow);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Flow>();
    }

    public async Task<Flow?> UpdateFlowAsync(string id, Flow flow)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/flows/{id}", flow);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<Flow>();
    }

    public async Task<bool> DeleteFlowAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/flows/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<NodeDefinition>?> GetNodeDefinitionsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<NodeDefinition>>("api/nodes/definitions");
    }

    // Dashboard APIs
    public async Task<List<DashboardConfig>?> GetAllDashboardsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<DashboardConfig>>("api/dashboards");
    }

    public async Task<DashboardConfig?> GetDashboardAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<DashboardConfig>($"api/dashboards/{id}");
    }

    public async Task<DashboardDataSnapshot?> GetDashboardDataAsync(string id)
    {
        return await _httpClient.GetFromJsonAsync<DashboardDataSnapshot>($"api/dashboards/{id}/data");
    }

    public async Task<DashboardConfig?> CreateDashboardAsync(DashboardConfig config)
    {
        var response = await _httpClient.PostAsJsonAsync("api/dashboards", config);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DashboardConfig>();
    }

    public async Task<DashboardConfig?> UpdateDashboardAsync(string id, DashboardConfig config)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/dashboards/{id}", config);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<DashboardConfig>();
    }

    public async Task<bool> DeleteDashboardAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/dashboards/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<DashboardTemplate>?> GetDashboardTemplatesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<DashboardTemplate>>("api/dashboards/templates/list");
    }

    public async Task<List<AlarmRecord>?> GetRecentAlarmsAsync(int count = 20)
    {
        return await _httpClient.GetFromJsonAsync<List<AlarmRecord>>($"api/dashboards/alarms/recent?count={count}");
    }

    public async Task<List<DeviceStatus>?> GetAllDevicesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<DeviceStatus>>("api/dashboards/devices/list");
    }
}
