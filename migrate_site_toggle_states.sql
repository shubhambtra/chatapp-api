-- ============================================
-- Migration: Add auto_reply_enabled and analysis_enabled columns to sites table
-- Safe to re-run: checks if columns exist before adding
-- ============================================

PRINT 'Starting sites toggle state migration...';

-- auto_reply_enabled
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'sites' AND COLUMN_NAME = 'auto_reply_enabled')
BEGIN
    ALTER TABLE dbo.sites ADD auto_reply_enabled BIT NOT NULL DEFAULT 0;
    PRINT 'Added column: auto_reply_enabled';
END
ELSE
    PRINT 'Column already exists: auto_reply_enabled';

-- analysis_enabled
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'sites' AND COLUMN_NAME = 'analysis_enabled')
BEGIN
    ALTER TABLE dbo.sites ADD analysis_enabled BIT NOT NULL DEFAULT 0;
    PRINT 'Added column: analysis_enabled';
END
ELSE
    PRINT 'Column already exists: analysis_enabled';

PRINT 'Sites toggle state migration completed!';
