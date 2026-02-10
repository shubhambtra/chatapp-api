using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class WorkflowService : IWorkflowService
{
    private readonly ApplicationDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WorkflowService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WorkflowDto>> GetWorkflowsAsync(string siteId)
    {
        var workflows = await _context.Workflows
            .Where(w => w.SiteId == siteId)
            .OrderBy(w => w.Priority)
            .ThenByDescending(w => w.CreatedAt)
            .ToListAsync();

        return workflows.Select(MapToDto).ToList();
    }

    public async Task<WorkflowDto?> GetWorkflowAsync(string workflowId)
    {
        var workflow = await _context.Workflows.FindAsync(workflowId);
        return workflow != null ? MapToDto(workflow) : null;
    }

    public async Task<List<WorkflowDto>> GetWorkflowsByTriggerAsync(string siteId, string triggerType)
    {
        var workflows = await _context.Workflows
            .Where(w => w.SiteId == siteId && w.TriggerType == triggerType && w.IsEnabled)
            .OrderBy(w => w.Priority)
            .ToListAsync();

        return workflows.Select(MapToDto).ToList();
    }

    public async Task<WorkflowDto> CreateWorkflowAsync(string siteId, CreateWorkflowRequest request, string? createdBy)
    {
        var workflow = new Workflow
        {
            SiteId = siteId,
            Name = request.Name,
            Description = request.Description,
            TriggerType = request.TriggerType,
            Conditions = JsonSerializer.Serialize(request.Conditions, JsonOptions),
            Actions = JsonSerializer.Serialize(request.Actions, JsonOptions),
            IsEnabled = request.IsEnabled,
            Priority = request.Priority,
            CreatedBy = createdBy
        };

        _context.Workflows.Add(workflow);
        await _context.SaveChangesAsync();

        return MapToDto(workflow);
    }

    public async Task<WorkflowDto> UpdateWorkflowAsync(string workflowId, UpdateWorkflowRequest request)
    {
        var workflow = await _context.Workflows.FindAsync(workflowId);
        if (workflow == null) throw new KeyNotFoundException("Workflow not found");

        if (request.Name != null) workflow.Name = request.Name;
        if (request.Description != null) workflow.Description = request.Description;
        if (request.TriggerType != null) workflow.TriggerType = request.TriggerType;
        if (request.Conditions != null) workflow.Conditions = JsonSerializer.Serialize(request.Conditions, JsonOptions);
        if (request.Actions != null) workflow.Actions = JsonSerializer.Serialize(request.Actions, JsonOptions);
        if (request.IsEnabled.HasValue) workflow.IsEnabled = request.IsEnabled.Value;
        if (request.Priority.HasValue) workflow.Priority = request.Priority.Value;

        await _context.SaveChangesAsync();

        return MapToDto(workflow);
    }

    public async Task DeleteWorkflowAsync(string workflowId)
    {
        var workflow = await _context.Workflows.FindAsync(workflowId);
        if (workflow == null) throw new KeyNotFoundException("Workflow not found");

        _context.Workflows.Remove(workflow);
        await _context.SaveChangesAsync();
    }

    private static WorkflowDto MapToDto(Workflow workflow)
    {
        List<WorkflowCondition> conditions = new();
        List<WorkflowAction> actions = new();

        try
        {
            if (!string.IsNullOrEmpty(workflow.Conditions) && workflow.Conditions != "[]")
                conditions = JsonSerializer.Deserialize<List<WorkflowCondition>>(workflow.Conditions, JsonOptions) ?? new();
        }
        catch { }

        try
        {
            if (!string.IsNullOrEmpty(workflow.Actions) && workflow.Actions != "[]")
                actions = JsonSerializer.Deserialize<List<WorkflowAction>>(workflow.Actions, JsonOptions) ?? new();
        }
        catch { }

        return new WorkflowDto
        {
            Id = workflow.Id,
            SiteId = workflow.SiteId,
            Name = workflow.Name,
            Description = workflow.Description,
            TriggerType = workflow.TriggerType,
            Conditions = conditions,
            Actions = actions,
            IsEnabled = workflow.IsEnabled,
            Priority = workflow.Priority,
            ExecutionCount = workflow.ExecutionCount,
            LastExecutedAt = workflow.LastExecutedAt,
            CreatedBy = workflow.CreatedBy,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt
        };
    }
}
