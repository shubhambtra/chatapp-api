using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface IWorkflowService
{
    Task<List<WorkflowDto>> GetWorkflowsAsync(string siteId);
    Task<WorkflowDto?> GetWorkflowAsync(string workflowId);
    Task<List<WorkflowDto>> GetWorkflowsByTriggerAsync(string siteId, string triggerType);
    Task<WorkflowDto> CreateWorkflowAsync(string siteId, CreateWorkflowRequest request, string? createdBy);
    Task<WorkflowDto> UpdateWorkflowAsync(string workflowId, UpdateWorkflowRequest request);
    Task DeleteWorkflowAsync(string workflowId);
}
