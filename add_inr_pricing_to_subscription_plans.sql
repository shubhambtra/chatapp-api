-- Add INR pricing fields to subscription_plans table
-- Run this migration to enable INR currency support for subscription plans

-- Add MonthlyPriceInr column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'subscription_plans') AND name = 'MonthlyPriceInr')
BEGIN
    ALTER TABLE subscription_plans ADD MonthlyPriceInr DECIMAL(18,2) NULL;
    PRINT 'Added MonthlyPriceInr column';
END
GO

-- Add AnnualPriceInr column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'subscription_plans') AND name = 'AnnualPriceInr')
BEGIN
    ALTER TABLE subscription_plans ADD AnnualPriceInr DECIMAL(18,2) NULL;
    PRINT 'Added AnnualPriceInr column';
END
GO

-- Add InrEnabled column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'subscription_plans') AND name = 'InrEnabled')
BEGIN
    ALTER TABLE subscription_plans ADD InrEnabled BIT NOT NULL DEFAULT 0;
    PRINT 'Added InrEnabled column';
END
GO

PRINT 'INR pricing columns added to subscription_plans table successfully.';
