using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/sites/{siteId}/[controller]")]
[Authorize]
public class WorkflowsController : ControllerBase
{
    private readonly IWorkflowService _workflowService;

    public WorkflowsController(IWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<WorkflowDto>>>> GetWorkflows(string siteId)
    {
        var workflows = await _workflowService.GetWorkflowsAsync(siteId);
        return Ok(ApiResponse<List<WorkflowDto>>.Ok(workflows));
    }

    [HttpGet("{workflowId}")]
    public async Task<ActionResult<ApiResponse<WorkflowDto>>> GetWorkflow(string siteId, string workflowId)
    {
        var workflow = await _workflowService.GetWorkflowAsync(workflowId);
        if (workflow == null)
            return NotFound(ApiResponse<WorkflowDto>.Fail("Workflow not found"));

        return Ok(ApiResponse<WorkflowDto>.Ok(workflow));
    }

    [HttpGet("trigger/{triggerType}")]
    public async Task<ActionResult<ApiResponse<List<WorkflowDto>>>> GetWorkflowsByTrigger(string siteId, string triggerType)
    {
        var workflows = await _workflowService.GetWorkflowsByTriggerAsync(siteId, triggerType);
        return Ok(ApiResponse<List<WorkflowDto>>.Ok(workflows));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<WorkflowDto>>> CreateWorkflow(
        string siteId,
        [FromBody] CreateWorkflowRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var workflow = await _workflowService.CreateWorkflowAsync(siteId, request, userId);
        return Ok(ApiResponse<WorkflowDto>.Ok(workflow, "Workflow created"));
    }

    [HttpPut("{workflowId}")]
    public async Task<ActionResult<ApiResponse<WorkflowDto>>> UpdateWorkflow(
        string siteId,
        string workflowId,
        [FromBody] UpdateWorkflowRequest request)
    {
        try
        {
            var workflow = await _workflowService.UpdateWorkflowAsync(workflowId, request);
            return Ok(ApiResponse<WorkflowDto>.Ok(workflow));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<WorkflowDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{workflowId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteWorkflow(string siteId, string workflowId)
    {
        try
        {
            await _workflowService.DeleteWorkflowAsync(workflowId);
            return Ok(ApiResponse<object>.Ok(null, "Workflow deleted"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
