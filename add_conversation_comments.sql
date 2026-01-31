-- Migration: Add conversation_comments table for internal agent notes
-- Date: 2026-01-26

-- Create conversation_comments table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='conversation_comments' AND xtype='U')
BEGIN
    CREATE TABLE dbo.conversation_comments (
        id NVARCHAR(36) PRIMARY KEY DEFAULT NEWID(),
        conversation_id NVARCHAR(36) NOT NULL,
        author_id NVARCHAR(36) NOT NULL,
        author_name NVARCHAR(255) NULL,
        content NVARCHAR(MAX) NOT NULL,
        mentions NVARCHAR(MAX) DEFAULT '[]',
        created_at DATETIME2 DEFAULT SYSDATETIME(),
        updated_at DATETIME2 DEFAULT SYSDATETIME(),
        CONSTRAINT FK_conversation_comments_conversations
            FOREIGN KEY (conversation_id)
            REFERENCES dbo.conversations(id)
            ON DELETE CASCADE
    );

    -- Create index for faster lookup by conversation
    CREATE NONCLUSTERED INDEX IX_conversation_comments_conversation_id
        ON dbo.conversation_comments(conversation_id);

    -- Create index for faster lookup by author
    CREATE NONCLUSTERED INDEX IX_conversation_comments_author_id
        ON dbo.conversation_comments(author_id);

    PRINT 'Created conversation_comments table';
END
ELSE
BEGIN
    PRINT 'conversation_comments table already exists';
END
GO
