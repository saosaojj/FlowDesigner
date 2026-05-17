using FlowDesigner.Shared.Models;
using System.Collections.Concurrent;

namespace FlowDesigner.Api.Services;

public class FlowService
{
    private readonly ConcurrentDictionary<string, Flow> _flows = new();

    public FlowService()
    {
        InitializeSampleFlows();
    }

    public Task<List<Flow>> GetAllFlowsAsync()
    {
        return Task.FromResult(_flows.Values.ToList());
    }

    public Task<Flow?> GetFlowAsync(string id)
    {
        _flows.TryGetValue(id, out var flow);
        return Task.FromResult(flow);
    }

    public Task<Flow> CreateFlowAsync(Flow flow)
    {
        flow.Id = Guid.NewGuid().ToString();
        flow.CreatedAt = DateTime.UtcNow;
        _flows[flow.Id] = flow;
        return Task.FromResult(flow);
    }

    public Task<Flow?> UpdateFlowAsync(string id, Flow flow)
    {
        if (!_flows.ContainsKey(id))
            return Task.FromResult<Flow?>(null);

        flow.Id = id;
        flow.UpdatedAt = DateTime.UtcNow;
        _flows[id] = flow;
        return Task.FromResult(flow);
    }

    public Task<bool> DeleteFlowAsync(string id)
    {
        var result = _flows.TryRemove(id, out _);
        return Task.FromResult(result);
    }

    private void InitializeSampleFlows()
    {
        var sampleFlow = new Flow
        {
            Name = "Sample Flow",
            Description = "A sample flow to demonstrate the flow designer",
            Nodes = new List<FlowNode>(),
            Connections = new List<FlowConnection>()
        };
        _flows[sampleFlow.Id] = sampleFlow;
    }
}
