namespace ChatApp.API.Models.Entities;

public class Workflow : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Trigger: new_message, customer_join, conversation_idle, sentiment_change
    public string TriggerType { get; set; } = string.Empty;

    // JSON arrays
    public string Conditions { get; set; } = "[]";
    public string Actions { get; set; } = "[]";

    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0;
    public int ExecutionCount { get; set; } = 0;
    public DateTime? LastExecutedAt { get; set; }

    public string? CreatedBy { get; set; }
    public User? Creator { get; set; }

    // Navigation
    public ICollection<WorkflowExecution> Executions { get; set; } = new List<WorkflowExecution>();
}

public class WorkflowExecution : BaseEntity
{
    public string WorkflowId { get; set; } = string.Empty;
    public Workflow Workflow { get; set; } = null!;

    public string? ConversationId { get; set; }
    public string? VisitorId { get; set; }
    public string? TriggerType { get; set; }
    public bool ConditionsMatched { get; set; }
    public string? ActionsExecuted { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
