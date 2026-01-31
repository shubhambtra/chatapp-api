-- Add auto-pay columns to subscriptions table
ALTER TABLE dbo.subscriptions ADD AutoPayEnabled BIT NOT NULL DEFAULT 0;
ALTER TABLE dbo.subscriptions ADD PreferredPaymentGateway NVARCHAR(50) NULL;
ALTER TABLE dbo.subscriptions ADD DefaultPaymentMethodId NVARCHAR(450) NULL;

-- Add recurring payment columns to payment_methods table
ALTER TABLE dbo.payment_methods ADD Gateway NVARCHAR(50) NULL;
ALTER TABLE dbo.payment_methods ADD RazorpayCustomerId NVARCHAR(255) NULL;
ALTER TABLE dbo.payment_methods ADD RazorpayTokenId NVARCHAR(255) NULL;
ALTER TABLE dbo.payment_methods ADD PayPalBillingAgreementId NVARCHAR(255) NULL;
ALTER TABLE dbo.payment_methods ADD PayPalPayerId NVARCHAR(255) NULL;

-- Create payment_logs table for debugging
CREATE TABLE dbo.payment_logs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SiteId NVARCHAR(450) NULL,
    SubscriptionId NVARCHAR(450) NULL,
    PaymentMethodId NVARCHAR(450) NULL,
    PaymentId NVARCHAR(450) NULL,
    Action NVARCHAR(100) NOT NULL,
    Gateway NVARCHAR(50) NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    RequestData NVARCHAR(MAX) NULL,
    ResponseData NVARCHAR(MAX) NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    ErrorCode NVARCHAR(100) NULL,
    StackTrace NVARCHAR(MAX) NULL,
    TransactionId NVARCHAR(255) NULL,
    OrderId NVARCHAR(255) NULL,
    Amount DECIMAL(18,2) NULL,
    Currency NVARCHAR(10) NULL,
    IpAddress NVARCHAR(50) NULL,
    UserAgent NVARCHAR(500) NULL,
    UserId NVARCHAR(450) NULL,
    Metadata NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DurationMs INT NULL
);

-- Add indexes for payment_logs
CREATE INDEX IX_payment_logs_SiteId ON dbo.payment_logs(SiteId);
CREATE INDEX IX_payment_logs_SubscriptionId ON dbo.payment_logs(SubscriptionId);
CREATE INDEX IX_payment_logs_Action ON dbo.payment_logs(Action);
CREATE INDEX IX_payment_logs_CreatedAt ON dbo.payment_logs(CreatedAt);

PRINT 'Auto-pay columns and payment_logs table added successfully';
