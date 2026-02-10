-- Workflows table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='workflows' AND xtype='U')
BEGIN
    CREATE TABLE dbo.workflows (
        id NVARCHAR(36) NOT NULL PRIMARY KEY,
        site_id NVARCHAR(36) NOT NULL,
        name NVARCHAR(255) NOT NULL,
        description NVARCHAR(MAX) NULL,
        trigger_type NVARCHAR(50) NOT NULL,  -- new_message, customer_join, conversation_idle, sentiment_change
        conditions NVARCHAR(MAX) NOT NULL DEFAULT '[]',  -- JSON array of {field, operator, value}
        actions NVARCHAR(MAX) NOT NULL DEFAULT '[]',      -- JSON array of {type, value}
        is_enabled BIT NOT NULL DEFAULT 1,
        priority INT NOT NULL DEFAULT 0,
        execution_count INT NOT NULL DEFAULT 0,
        last_executed_at DATETIME2 NULL,
        created_by NVARCHAR(36) NULL,
        created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        updated_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_workflows_site FOREIGN KEY (site_id) REFERENCES dbo.sites(Id) ON DELETE CASCADE,
        CONSTRAINT FK_workflows_created_by FOREIGN KEY (created_by) REFERENCES dbo.users(Id) ON DELETE SET NULL
    );

    CREATE INDEX IX_workflows_site_id ON dbo.workflows(site_id);
    CREATE INDEX IX_workflows_trigger_type ON dbo.workflows(trigger_type);
    CREATE INDEX IX_workflows_is_enabled ON dbo.workflows(is_enabled);
END
GO

-- Workflow executions log table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='workflow_executions' AND xtype='U')
BEGIN
    CREATE TABLE dbo.workflow_executions (
        id NVARCHAR(36) NOT NULL PRIMARY KEY,
        workflow_id NVARCHAR(36) NOT NULL,
        conversation_id NVARCHAR(36) NULL,
        visitor_id NVARCHAR(36) NULL,
        trigger_type NVARCHAR(50) NULL,
        conditions_matched BIT NOT NULL DEFAULT 0,
        actions_executed NVARCHAR(MAX) NULL,
        error_message NVARCHAR(MAX) NULL,
        executed_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        updated_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_workflow_executions_workflow FOREIGN KEY (workflow_id) REFERENCES dbo.workflows(id) ON DELETE CASCADE
    );

    CREATE INDEX IX_workflow_executions_workflow_id ON dbo.workflow_executions(workflow_id);
    CREATE INDEX IX_workflow_executions_executed_at ON dbo.workflow_executions(executed_at);
END
GO
