using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("workflows");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasMaxLength(36);

        builder.Property(w => w.SiteId).IsRequired().HasMaxLength(36).HasColumnName("site_id");
        builder.Property(w => w.Name).IsRequired().HasMaxLength(255).HasColumnName("name");
        builder.Property(w => w.Description).HasColumnType("nvarchar(max)").HasColumnName("description");
        builder.Property(w => w.TriggerType).IsRequired().HasMaxLength(36).HasColumnName("trigger_type");
        builder.Property(w => w.Conditions).HasColumnType("nvarchar(max)").HasDefaultValue("[]").HasColumnName("conditions");
        builder.Property(w => w.Actions).HasColumnType("nvarchar(max)").HasDefaultValue("[]").HasColumnName("actions");
        builder.Property(w => w.IsEnabled).HasDefaultValue(true).HasColumnName("is_enabled");
        builder.Property(w => w.Priority).HasDefaultValue(0).HasColumnName("priority");
        builder.Property(w => w.ExecutionCount).HasDefaultValue(0).HasColumnName("execution_count");
        builder.Property(w => w.LastExecutedAt).HasColumnName("last_executed_at");
        builder.Property(w => w.CreatedBy).HasMaxLength(36).HasColumnName("created_by");
        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.Id).HasColumnName("id");

        builder.HasOne(w => w.Site)
            .WithMany()
            .HasForeignKey(w => w.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Creator)
            .WithMany()
            .HasForeignKey(w => w.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(w => w.SiteId);
        builder.HasIndex(w => w.TriggerType);
        builder.HasIndex(w => w.IsEnabled);
    }
}

public class WorkflowExecutionConfiguration : IEntityTypeConfiguration<WorkflowExecution>
{
    public void Configure(EntityTypeBuilder<WorkflowExecution> builder)
    {
        builder.ToTable("workflow_executions");

        builder.HasKey(we => we.Id);
        builder.Property(we => we.Id).HasMaxLength(36).HasColumnName("id");

        builder.Property(we => we.WorkflowId).IsRequired().HasMaxLength(36).HasColumnName("workflow_id");
        builder.Property(we => we.ConversationId).HasMaxLength(36).HasColumnName("conversation_id");
        builder.Property(we => we.VisitorId).HasMaxLength(36).HasColumnName("visitor_id");
        builder.Property(we => we.TriggerType).HasMaxLength(36).HasColumnName("trigger_type");
        builder.Property(we => we.ConditionsMatched).HasDefaultValue(false).HasColumnName("conditions_matched");
        builder.Property(we => we.ActionsExecuted).HasColumnType("nvarchar(max)").HasColumnName("actions_executed");
        builder.Property(we => we.ErrorMessage).HasColumnType("nvarchar(max)").HasColumnName("error_message");
        builder.Property(we => we.ExecutedAt).HasColumnName("executed_at");
        builder.Property(we => we.CreatedAt).HasColumnName("created_at");
        builder.Property(we => we.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(we => we.Workflow)
            .WithMany(w => w.Executions)
            .HasForeignKey(we => we.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(we => we.WorkflowId);
        builder.HasIndex(we => we.ExecutedAt);
    }
}
