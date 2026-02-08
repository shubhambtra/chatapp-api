-- Add DeliveredAt column to messages table for read receipt support
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('messages') AND name = 'DeliveredAt')
BEGIN
    ALTER TABLE [dbo].[messages] ADD [DeliveredAt] DATETIME2 NULL;
    PRINT 'Column DeliveredAt added to messages table.';
END
ELSE
BEGIN
    PRINT 'Column DeliveredAt already exists on messages table.';
END
GO
