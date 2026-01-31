-- Migration Script: Add Demo Requests table
-- Date: 2026-01-31
-- Description: Creates demo_requests table for Request a Demo feature

IF OBJECT_ID('dbo.demo_requests', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.demo_requests (
        id NVARCHAR(50) NOT NULL PRIMARY KEY,
        name NVARCHAR(255) NOT NULL,
        email NVARCHAR(255) NOT NULL,
        company NVARCHAR(255) NOT NULL,
        phone NVARCHAR(50) NULL,
        message NVARCHAR(MAX) NULL,
        status NVARCHAR(50) NOT NULL DEFAULT 'pending',
        admin_notes NVARCHAR(MAX) NULL,
        ip_address NVARCHAR(100) NULL,
        user_agent NVARCHAR(500) NULL,
        created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        updated_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_demo_requests_status ON dbo.demo_requests (status);
    CREATE INDEX IX_demo_requests_created_at ON dbo.demo_requests (created_at DESC);

    PRINT 'Created demo_requests table';
END
ELSE
BEGIN
    PRINT 'demo_requests table already exists';
END
