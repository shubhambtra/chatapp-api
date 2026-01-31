-- Migration Script: Add Password Reset Fields to Users table
-- Date: 2026-01-22
-- Description: Adds PasswordResetToken and PasswordResetTokenExpiresAt columns for forgot password functionality

-- Check if columns already exist before adding
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordResetToken')
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [PasswordResetToken] NVARCHAR(255) NULL;
    PRINT 'Added PasswordResetToken column to Users table';
END
ELSE
BEGIN
    PRINT 'PasswordResetToken column already exists';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordResetTokenExpiresAt')
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [PasswordResetTokenExpiresAt] DATETIME2 NULL;
    PRINT 'Added PasswordResetTokenExpiresAt column to Users table';
END
ELSE
BEGIN
    PRINT 'PasswordResetTokenExpiresAt column already exists';
END

-- Create index for faster token lookup
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_PasswordResetToken' AND object_id = OBJECT_ID('Users'))
BEGIN
    CREATE INDEX [IX_Users_PasswordResetToken] ON [dbo].[Users] ([PasswordResetToken]) WHERE [PasswordResetToken] IS NOT NULL;
    PRINT 'Created index IX_Users_PasswordResetToken';
END
ELSE
BEGIN
    PRINT 'Index IX_Users_PasswordResetToken already exists';
END

PRINT 'Migration completed successfully';
