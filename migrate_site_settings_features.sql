-- ============================================
-- Migration: Add feature flag columns and support_address to site_settings
-- Safe to re-run: checks if columns exist before adding
-- ============================================

PRINT 'Starting site_settings feature flags migration...';

-- support_address
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'support_address')
BEGIN
    ALTER TABLE dbo.site_settings ADD support_address NVARCHAR(255) NULL;
    PRINT 'Added column: support_address';
END

-- feature_supervisor_mode
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_supervisor_mode')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_supervisor_mode BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_supervisor_mode';
END

-- feature_ai_analysis
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_ai_analysis')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_ai_analysis BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_ai_analysis';
END

-- feature_ai_auto_reply
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_ai_auto_reply')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_ai_auto_reply BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_ai_auto_reply';
END

-- feature_file_sharing
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_file_sharing')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_file_sharing BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_file_sharing';
END

-- feature_csat_ratings
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_csat_ratings')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_csat_ratings BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_csat_ratings';
END

-- feature_visitor_info
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_visitor_info')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_visitor_info BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_visitor_info';
END

-- feature_canned_responses
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_canned_responses')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_canned_responses BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_canned_responses';
END

-- feature_conversation_transfer
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_conversation_transfer')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_conversation_transfer BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_conversation_transfer';
END

-- feature_team_chat
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_team_chat')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_team_chat BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_team_chat';
END

-- feature_typing_indicators
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_typing_indicators')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_typing_indicators BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_typing_indicators';
END

-- feature_read_receipts
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_read_receipts')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_read_receipts BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_read_receipts';
END

-- feature_internal_notes
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_internal_notes')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_internal_notes BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_internal_notes';
END

-- feature_emoji_picker
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_emoji_picker')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_emoji_picker BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_emoji_picker';
END

-- feature_email_sending
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_email_sending')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_email_sending BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_email_sending';
END

-- feature_conversation_search
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_conversation_search')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_conversation_search BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_conversation_search';
END

-- feature_message_search
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_message_search')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_message_search BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_message_search';
END

-- feature_bulk_actions
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_bulk_actions')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_bulk_actions BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_bulk_actions';
END

-- feature_themes
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_themes')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_themes BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_themes';
END

-- feature_agent_status
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_agent_status')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_agent_status BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_agent_status';
END

-- feature_notifications
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'site_settings' AND COLUMN_NAME = 'feature_notifications')
BEGIN
    ALTER TABLE dbo.site_settings ADD feature_notifications BIT NOT NULL DEFAULT 1;
    PRINT 'Added column: feature_notifications';
END

PRINT 'Migration completed successfully!';
GO
