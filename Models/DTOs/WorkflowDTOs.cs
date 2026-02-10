namespace ChatApp.API.Models.DTOs;

public class WorkflowDto
{
    public string Id { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public List<WorkflowCondition> Conditions { get; set; } = new();
    public List<WorkflowAction> Actions { get; set; } = new();
    public bool IsEnabled { get; set; }
    public int Priority { get; set; }
    public int ExecutionCount { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateWorkflowRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public List<WorkflowCondition> Conditions { get; set; } = new();
    public List<WorkflowAction> Actions { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0;
}

public class UpdateWorkflowRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? TriggerType { get; set; }
    public List<WorkflowCondition>? Conditions { get; set; }
    public List<WorkflowAction>? Actions { get; set; }
    public bool? IsEnabled { get; set; }
    public int? Priority { get; set; }
}

public class WorkflowCondition
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class WorkflowAction
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
