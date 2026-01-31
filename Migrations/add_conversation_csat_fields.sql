-- Migration: Add CSAT and Resolution fields to Conversations table
-- Date: 2026-01-25
-- Description: Adds ResolutionStatus and ClosingNote columns for close conversation feature

-- Add ResolutionStatus column
IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
    AND TABLE_NAME = 'conversations'
    AND COLUMN_NAME = 'ResolutionStatus'
)
BEGIN
    ALTER TABLE dbo.conversations
    ADD ResolutionStatus NVARCHAR(50) NULL;
    PRINT 'Added ResolutionStatus column';
END
ELSE
BEGIN
    PRINT 'ResolutionStatus column already exists';
END
GO

-- Add ClosingNote column
IF NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
    AND TABLE_NAME = 'conversations'
    AND COLUMN_NAME = 'ClosingNote'
)
BEGIN
    ALTER TABLE dbo.conversations
    ADD ClosingNote NVARCHAR(MAX) NULL;
    PRINT 'Added ClosingNote column';
END
ELSE
BEGIN
    PRINT 'ClosingNote column already exists';
END
GO

-- Update existing closed conversations to have a default resolution status
UPDATE dbo.conversations
SET ResolutionStatus = 'resolved'
WHERE Status = 'closed' AND ResolutionStatus IS NULL;
GO

PRINT 'Migration completed successfully';
