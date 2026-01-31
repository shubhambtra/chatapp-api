-- =============================================
-- Email Logs Table
-- Created: 2026-01-21
-- Description: Stores all sent email logs for admin visibility
-- =============================================

CREATE TABLE [email_logs] (
    [Id] NVARCHAR(36) NOT NULL PRIMARY KEY,
    [FromEmail] NVARCHAR(255) NOT NULL,
    [FromName] NVARCHAR(255) NULL,
    [ToEmail] NVARCHAR(255) NOT NULL,
    [ToName] NVARCHAR(255) NULL,
    [Subject] NVARCHAR(500) NOT NULL,
    [Body] NVARCHAR(MAX) NOT NULL,
    [IsHtml] BIT NOT NULL DEFAULT 1,
    [Status] NVARCHAR(50) NOT NULL DEFAULT 'sent',  -- sent, failed, pending
    [ErrorMessage] NVARCHAR(MAX) NULL,
    [SiteId] NVARCHAR(36) NULL,
    [UserId] NVARCHAR(36) NULL,
    [EmailType] NVARCHAR(100) NULL,  -- welcome, password_reset, subscription_expiry, etc.
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [SentAt] DATETIME2 NULL
);

-- Indexes for faster querying
CREATE INDEX [IX_email_logs_CreatedAt] ON [email_logs] ([CreatedAt] DESC);
CREATE INDEX [IX_email_logs_ToEmail] ON [email_logs] ([ToEmail]);
CREATE INDEX [IX_email_logs_Status] ON [email_logs] ([Status]);
CREATE INDEX [IX_email_logs_EmailType] ON [email_logs] ([EmailType]);
CREATE INDEX [IX_email_logs_SiteId] ON [email_logs] ([SiteId]);
