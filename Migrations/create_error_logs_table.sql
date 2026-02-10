-- Create error_logs table for centralized error logging
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'error_logs')
BEGIN
    CREATE TABLE [dbo].[error_logs] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [ErrorMessage] NVARCHAR(MAX) NOT NULL,
        [StackTrace] NVARCHAR(MAX) NULL,
        [Source] NVARCHAR(500) NULL,
        [ErrorCode] NVARCHAR(50) NULL,
        [RequestPath] NVARCHAR(500) NULL,
        [RequestMethod] NVARCHAR(10) NULL,
        [RequestBody] NVARCHAR(MAX) NULL,
        [QueryString] NVARCHAR(2000) NULL,
        [UserId] NVARCHAR(36) NULL,
        [IpAddress] NVARCHAR(50) NULL,
        [UserAgent] NVARCHAR(500) NULL,
        [ExceptionType] NVARCHAR(255) NULL,
        [InnerException] NVARCHAR(MAX) NULL,
        [Severity] NVARCHAR(20) NOT NULL DEFAULT 'Error',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_error_logs] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_error_logs_CreatedAt] ON [dbo].[error_logs] ([CreatedAt] DESC);
    CREATE NONCLUSTERED INDEX [IX_error_logs_RequestPath] ON [dbo].[error_logs] ([RequestPath]);
    CREATE NONCLUSTERED INDEX [IX_error_logs_UserId] ON [dbo].[error_logs] ([UserId]);

    PRINT 'Table error_logs created successfully.';
END
ELSE
BEGIN
    PRINT 'Table error_logs already exists.';
END
GO
