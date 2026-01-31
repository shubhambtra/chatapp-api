-- Migration: Create tutorial_videos table
-- Run this script on both databases

-- Create the tutorial_videos table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tutorial_videos' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[tutorial_videos] (
        [id] NVARCHAR(450) NOT NULL PRIMARY KEY,
        [title] NVARCHAR(200) NOT NULL,
        [description] NVARCHAR(1000) NULL,
        [youtube_url] NVARCHAR(500) NOT NULL,
        [thumbnail_url] NVARCHAR(500) NULL,
        [duration] NVARCHAR(20) NULL,
        [category] NVARCHAR(100) NULL,
        [display_order] INT NOT NULL DEFAULT 0,
        [is_active] BIT NOT NULL DEFAULT 1,
        [is_featured] BIT NOT NULL DEFAULT 0,
        [created_at] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [updated_at] DATETIME2 NULL
    );

    -- Create indexes
    CREATE INDEX [IX_tutorial_videos_display_order] ON [dbo].[tutorial_videos] ([display_order]);
    CREATE INDEX [IX_tutorial_videos_is_active] ON [dbo].[tutorial_videos] ([is_active]);
    CREATE INDEX [IX_tutorial_videos_category] ON [dbo].[tutorial_videos] ([category]);

    PRINT 'Table tutorial_videos created successfully';
END
ELSE
BEGIN
    PRINT 'Table tutorial_videos already exists';
END
GO
