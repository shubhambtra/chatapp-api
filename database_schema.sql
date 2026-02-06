-- ============================================
-- Chat Application Database Schema
-- ============================================
-- Database: Microsoft SQL Server
-- Version: 2.0 (with Subscription & Billing)
-- ============================================

-- Create Database (uncomment if needed)
-- CREATE DATABASE ChatAppDB;
-- GO
-- USE ChatAppDB;
-- GO

-- ============================================
-- DROP EXISTING TABLES (in reverse dependency order)
-- ============================================

-- Billing & Subscription tables
IF OBJECT_ID('dbo.usage_records', 'U') IS NOT NULL DROP TABLE dbo.usage_records;
IF OBJECT_ID('dbo.payment_refunds', 'U') IS NOT NULL DROP TABLE dbo.payment_refunds;
IF OBJECT_ID('dbo.payments', 'U') IS NOT NULL DROP TABLE dbo.payments;
IF OBJECT_ID('dbo.invoice_items', 'U') IS NOT NULL DROP TABLE dbo.invoice_items;
IF OBJECT_ID('dbo.invoices', 'U') IS NOT NULL DROP TABLE dbo.invoices;
IF OBJECT_ID('dbo.subscription_history', 'U') IS NOT NULL DROP TABLE dbo.subscription_history;
IF OBJECT_ID('dbo.subscriptions', 'U') IS NOT NULL DROP TABLE dbo.subscriptions;
IF OBJECT_ID('dbo.coupon_redemptions', 'U') IS NOT NULL DROP TABLE dbo.coupon_redemptions;
IF OBJECT_ID('dbo.coupons', 'U') IS NOT NULL DROP TABLE dbo.coupons;
IF OBJECT_ID('dbo.payment_methods', 'U') IS NOT NULL DROP TABLE dbo.payment_methods;
IF OBJECT_ID('dbo.plan_features', 'U') IS NOT NULL DROP TABLE dbo.plan_features;
IF OBJECT_ID('dbo.features', 'U') IS NOT NULL DROP TABLE dbo.features;
IF OBJECT_ID('dbo.subscription_plans', 'U') IS NOT NULL DROP TABLE dbo.subscription_plans;

-- Core application tables
IF OBJECT_ID('dbo.notification_reads', 'U') IS NOT NULL DROP TABLE dbo.notification_reads;
IF OBJECT_ID('dbo.notifications', 'U') IS NOT NULL DROP TABLE dbo.notifications;
IF OBJECT_ID('dbo.message_reads', 'U') IS NOT NULL DROP TABLE dbo.message_reads;
IF OBJECT_ID('dbo.messages', 'U') IS NOT NULL DROP TABLE dbo.messages;
IF OBJECT_ID('dbo.conversation_analysis', 'U') IS NOT NULL DROP TABLE dbo.conversation_analysis;
IF OBJECT_ID('dbo.conversations', 'U') IS NOT NULL DROP TABLE dbo.conversations;
IF OBJECT_ID('dbo.files', 'U') IS NOT NULL DROP TABLE dbo.files;
IF OBJECT_ID('dbo.visitor_sessions', 'U') IS NOT NULL DROP TABLE dbo.visitor_sessions;
IF OBJECT_ID('dbo.visitors', 'U') IS NOT NULL DROP TABLE dbo.visitors;
IF OBJECT_ID('dbo.agent_sessions', 'U') IS NOT NULL DROP TABLE dbo.agent_sessions;
IF OBJECT_ID('dbo.refresh_tokens', 'U') IS NOT NULL DROP TABLE dbo.refresh_tokens;
IF OBJECT_ID('dbo.user_sites', 'U') IS NOT NULL DROP TABLE dbo.user_sites;
IF OBJECT_ID('dbo.typing_indicators', 'U') IS NOT NULL DROP TABLE dbo.typing_indicators;
IF OBJECT_ID('dbo.canned_responses', 'U') IS NOT NULL DROP TABLE dbo.canned_responses;
IF OBJECT_ID('dbo.audit_logs', 'U') IS NOT NULL DROP TABLE dbo.audit_logs;
IF OBJECT_ID('dbo.agent_analytics_daily', 'U') IS NOT NULL DROP TABLE dbo.agent_analytics_daily;
IF OBJECT_ID('dbo.analytics_daily', 'U') IS NOT NULL DROP TABLE dbo.analytics_daily;
IF OBJECT_ID('dbo.users', 'U') IS NOT NULL DROP TABLE dbo.users;
IF OBJECT_ID('dbo.sites', 'U') IS NOT NULL DROP TABLE dbo.sites;
GO

-- ============================================
-- 1. SITES TABLE
-- ============================================
-- Stores information about each website/client using the chat widget

CREATE TABLE dbo.sites (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('site_' + LOWER(CONVERT(NVARCHAR(36), NEWID()))),
    name NVARCHAR(255) NOT NULL,
    domain NVARCHAR(255) NOT NULL,
    api_key NVARCHAR(64) NOT NULL UNIQUE DEFAULT (LOWER(CONVERT(NVARCHAR(64), HASHBYTES('SHA2_256', CONVERT(NVARCHAR(100), NEWID())), 2))),

    -- Owner information
    owner_user_id NVARCHAR(50) NULL, -- Primary account owner

    -- Company/Organization details
    company_name NVARCHAR(255) NULL,
    company_website NVARCHAR(255) NULL,
    company_size NVARCHAR(50) NULL, -- 1-10, 11-50, 51-200, 201-500, 500+
    industry NVARCHAR(100) NULL,

    -- Billing contact
    billing_email NVARCHAR(255) NULL,
    billing_name NVARCHAR(255) NULL,
    billing_phone NVARCHAR(50) NULL,
    billing_address_line1 NVARCHAR(255) NULL,
    billing_address_line2 NVARCHAR(255) NULL,
    billing_city NVARCHAR(100) NULL,
    billing_state NVARCHAR(100) NULL,
    billing_postal_code NVARCHAR(20) NULL,
    billing_country NVARCHAR(2) NULL, -- ISO country code

    -- Tax information
    tax_id NVARCHAR(50) NULL, -- VAT number, GST, etc.
    tax_exempt BIT DEFAULT 0,

    -- Stripe customer reference
    stripe_customer_id NVARCHAR(100) NULL,

    -- Widget configuration (stored as JSON string)
    widget_config NVARCHAR(MAX) DEFAULT N'{
        "primary_color": "#2563eb",
        "welcome_message": "Hi! How can we help you?",
        "offline_message": "We are currently offline. Leave a message!",
        "position": "bottom-right",
        "show_branding": true
    }',

    -- Settings
    status NVARCHAR(20) DEFAULT 'active' CHECK (status IN ('active', 'inactive', 'suspended')),
    timezone NVARCHAR(50) DEFAULT 'UTC',
    business_hours NVARCHAR(MAX) NULL, -- JSON

    -- AI settings
    ai_enabled BIT DEFAULT 1,
    ai_model NVARCHAR(50) DEFAULT 'gpt-4o-mini',

    -- AI toggle states (per-site operational settings)
    auto_reply_enabled BIT DEFAULT 0,
    analysis_enabled BIT DEFAULT 0,

    -- File settings
    max_file_size_mb INT DEFAULT 10,
    allowed_file_types NVARCHAR(500) DEFAULT '.jpg,.jpeg,.png,.gif,.webp,.pdf,.doc,.docx,.txt,.zip',

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME()
);

CREATE INDEX idx_sites_domain ON dbo.sites(domain);
CREATE INDEX idx_sites_status ON dbo.sites(status);
CREATE INDEX idx_sites_api_key ON dbo.sites(api_key);
CREATE INDEX idx_sites_stripe_customer_id ON dbo.sites(stripe_customer_id);
CREATE INDEX idx_sites_owner_user_id ON dbo.sites(owner_user_id);
GO

-- ============================================
-- 2. SUBSCRIPTION PLANS TABLE
-- ============================================
-- Defines available subscription tiers

CREATE TABLE dbo.subscription_plans (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('plan_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    name NVARCHAR(100) NOT NULL, -- Free, Starter, Professional, Enterprise
    code NVARCHAR(50) NOT NULL UNIQUE, -- free, starter, pro, enterprise
    description NVARCHAR(500) NULL,

    -- Pricing
    price_monthly DECIMAL(10,2) NOT NULL DEFAULT 0,
    price_yearly DECIMAL(10,2) NOT NULL DEFAULT 0, -- Usually discounted
    currency NVARCHAR(3) DEFAULT 'USD',

    -- Limits
    max_agents INT DEFAULT 1, -- Number of support agents allowed
    max_conversations_per_month INT DEFAULT 100, -- NULL = unlimited
    max_messages_per_month INT NULL, -- NULL = unlimited
    max_sites INT DEFAULT 1, -- Number of sites/widgets allowed
    max_file_storage_mb INT DEFAULT 100, -- File storage limit
    max_file_size_mb INT DEFAULT 5, -- Max single file size
    message_history_days INT DEFAULT 30, -- How long to keep messages

    -- Feature flags (JSON for flexibility)
    features NVARCHAR(MAX) DEFAULT N'{}',
    /*
    features example:
    {
        "ai_analysis": false,
        "custom_branding": false,
        "remove_powered_by": false,
        "priority_support": false,
        "api_access": false,
        "webhooks": false,
        "integrations": [],
        "export_data": false,
        "advanced_analytics": false,
        "custom_domain": false,
        "sla_guarantee": false
    }
    */

    -- Trial settings
    trial_days INT DEFAULT 14, -- 0 = no trial

    -- Display
    display_order INT DEFAULT 0,
    is_popular BIT DEFAULT 0, -- Highlight as "Most Popular"
    is_active BIT DEFAULT 1, -- Can new customers subscribe
    is_public BIT DEFAULT 1, -- Show on pricing page

    -- Stripe/Payment Gateway IDs
    stripe_price_id_monthly NVARCHAR(100) NULL,
    stripe_price_id_yearly NVARCHAR(100) NULL,
    stripe_product_id NVARCHAR(100) NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME()
);

CREATE INDEX idx_subscription_plans_code ON dbo.subscription_plans(code);
CREATE INDEX idx_subscription_plans_is_active ON dbo.subscription_plans(is_active);
GO

-- ============================================
-- 3. FEATURES TABLE
-- ============================================
-- Master list of all available features

CREATE TABLE dbo.features (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('feat_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    code NVARCHAR(50) NOT NULL UNIQUE, -- ai_analysis, custom_branding, etc.
    name NVARCHAR(100) NOT NULL,
    description NVARCHAR(500) NULL,
    category NVARCHAR(50) NULL, -- core, analytics, integrations, support, customization

    -- For metered features
    is_metered BIT DEFAULT 0, -- Is this a usage-based feature?
    unit_name NVARCHAR(50) NULL, -- messages, api_calls, ai_requests

    -- Display
    display_order INT DEFAULT 0,
    is_active BIT DEFAULT 1,

    created_at DATETIME2 DEFAULT SYSDATETIME()
);

CREATE INDEX idx_features_code ON dbo.features(code);
CREATE INDEX idx_features_category ON dbo.features(category);
GO

-- ============================================
-- 4. PLAN FEATURES TABLE (Many-to-Many)
-- ============================================
-- Links features to plans with optional limits

CREATE TABLE dbo.plan_features (
    id INT IDENTITY(1,1) PRIMARY KEY,
    plan_id NVARCHAR(50) NOT NULL,
    feature_id NVARCHAR(50) NOT NULL,

    -- Feature configuration for this plan
    is_enabled BIT DEFAULT 1,
    limit_value INT NULL, -- For metered features, NULL = unlimited

    created_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_plan_features_plan FOREIGN KEY (plan_id) REFERENCES dbo.subscription_plans(id) ON DELETE CASCADE,
    CONSTRAINT FK_plan_features_feature FOREIGN KEY (feature_id) REFERENCES dbo.features(id) ON DELETE CASCADE,
    CONSTRAINT UQ_plan_features UNIQUE (plan_id, feature_id)
);

CREATE INDEX idx_plan_features_plan_id ON dbo.plan_features(plan_id);
CREATE INDEX idx_plan_features_feature_id ON dbo.plan_features(feature_id);
GO

-- ============================================
-- 5. PAYMENT METHODS TABLE
-- ============================================
-- Stores customer payment methods (tokenized)

CREATE TABLE dbo.payment_methods (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('pm_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    site_id NVARCHAR(50) NOT NULL,

    -- Payment method type
    type NVARCHAR(20) NOT NULL DEFAULT 'card', -- card, bank_account, paypal, etc.

    -- Card details (tokenized - never store full card numbers!)
    card_brand NVARCHAR(20) NULL, -- visa, mastercard, amex, etc.
    card_last_four NVARCHAR(4) NULL,
    card_exp_month INT NULL,
    card_exp_year INT NULL,
    card_holder_name NVARCHAR(255) NULL,

    -- Bank account details (for ACH)
    bank_name NVARCHAR(100) NULL,
    bank_last_four NVARCHAR(4) NULL,

    -- Billing address
    billing_name NVARCHAR(255) NULL,
    billing_email NVARCHAR(255) NULL,
    billing_phone NVARCHAR(50) NULL,
    billing_address_line1 NVARCHAR(255) NULL,
    billing_address_line2 NVARCHAR(255) NULL,
    billing_city NVARCHAR(100) NULL,
    billing_state NVARCHAR(100) NULL,
    billing_postal_code NVARCHAR(20) NULL,
    billing_country NVARCHAR(2) NULL, -- ISO country code

    -- Payment gateway references
    stripe_payment_method_id NVARCHAR(100) NULL,
    stripe_customer_id NVARCHAR(100) NULL,

    -- Status
    is_default BIT DEFAULT 0,
    is_verified BIT DEFAULT 0,
    is_active BIT DEFAULT 1,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_payment_methods_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE CASCADE
);

CREATE INDEX idx_payment_methods_site_id ON dbo.payment_methods(site_id);
CREATE INDEX idx_payment_methods_stripe_customer_id ON dbo.payment_methods(stripe_customer_id);
CREATE INDEX idx_payment_methods_is_default ON dbo.payment_methods(is_default);
GO

-- ============================================
-- 6. COUPONS TABLE
-- ============================================
-- Promotional codes and discounts

CREATE TABLE dbo.coupons (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('coupon_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    code NVARCHAR(50) NOT NULL UNIQUE, -- The actual coupon code users enter
    name NVARCHAR(100) NOT NULL,
    description NVARCHAR(500) NULL,

    -- Discount type
    discount_type NVARCHAR(20) NOT NULL DEFAULT 'percentage', -- percentage, fixed_amount
    discount_value DECIMAL(10,2) NOT NULL, -- 20 for 20% or 20.00 for $20
    currency NVARCHAR(3) DEFAULT 'USD', -- For fixed_amount type

    -- Applicability
    applies_to NVARCHAR(20) DEFAULT 'all', -- all, specific_plans
    applicable_plan_ids NVARCHAR(MAX) NULL, -- JSON array of plan IDs if applies_to = 'specific_plans'

    -- Duration
    duration NVARCHAR(20) DEFAULT 'once', -- once, repeating, forever
    duration_months INT NULL, -- For repeating: number of months

    -- Limits
    max_redemptions INT NULL, -- NULL = unlimited
    max_redemptions_per_site INT DEFAULT 1,
    current_redemptions INT DEFAULT 0,

    -- Validity period
    valid_from DATETIME2 DEFAULT SYSDATETIME(),
    valid_until DATETIME2 NULL, -- NULL = no expiry

    -- Minimum requirements
    min_amount DECIMAL(10,2) NULL, -- Minimum order amount
    first_time_only BIT DEFAULT 0, -- Only for new subscriptions

    -- Stripe reference
    stripe_coupon_id NVARCHAR(100) NULL,

    -- Status
    is_active BIT DEFAULT 1,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME()
);

CREATE INDEX idx_coupons_code ON dbo.coupons(code);
CREATE INDEX idx_coupons_is_active ON dbo.coupons(is_active);
CREATE INDEX idx_coupons_valid_until ON dbo.coupons(valid_until);
GO

-- ============================================
-- 7. SUBSCRIPTIONS TABLE
-- ============================================
-- Active subscriptions linking sites to plans

CREATE TABLE dbo.subscriptions (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('sub_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    site_id NVARCHAR(50) NOT NULL,
    plan_id NVARCHAR(50) NOT NULL,

    -- Subscription details
    status NVARCHAR(20) NOT NULL DEFAULT 'active',
    -- active, trialing, past_due, canceled, unpaid, incomplete, incomplete_expired, paused

    -- Billing cycle
    billing_cycle NVARCHAR(20) DEFAULT 'monthly', -- monthly, yearly

    -- Current period
    current_period_start DATETIME2 NOT NULL,
    current_period_end DATETIME2 NOT NULL,

    -- Trial info
    trial_start DATETIME2 NULL,
    trial_end DATETIME2 NULL,
    is_trial BIT DEFAULT 0,

    -- Cancellation
    cancel_at_period_end BIT DEFAULT 0, -- Will cancel at end of current period
    canceled_at DATETIME2 NULL,
    cancellation_reason NVARCHAR(500) NULL,

    -- Pause info
    paused_at DATETIME2 NULL,
    resume_at DATETIME2 NULL,

    -- Applied coupon
    coupon_id NVARCHAR(50) NULL,
    discount_end_date DATETIME2 NULL,

    -- Payment
    default_payment_method_id NVARCHAR(50) NULL,

    -- Stripe references
    stripe_subscription_id NVARCHAR(100) NULL,
    stripe_customer_id NVARCHAR(100) NULL,

    -- Pricing at time of subscription (in case plan prices change)
    price_at_subscription DECIMAL(10,2) NOT NULL,
    currency NVARCHAR(3) DEFAULT 'USD',

    -- Usage tracking
    conversations_used_this_period INT DEFAULT 0,
    messages_used_this_period INT DEFAULT 0,
    storage_used_mb DECIMAL(10,2) DEFAULT 0,

    -- Timestamps
    started_at DATETIME2 DEFAULT SYSDATETIME(),
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_subscriptions_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE CASCADE,
    CONSTRAINT FK_subscriptions_plan FOREIGN KEY (plan_id) REFERENCES dbo.subscription_plans(id),
    CONSTRAINT FK_subscriptions_coupon FOREIGN KEY (coupon_id) REFERENCES dbo.coupons(id),
    CONSTRAINT FK_subscriptions_payment_method FOREIGN KEY (default_payment_method_id) REFERENCES dbo.payment_methods(id)
);

CREATE INDEX idx_subscriptions_site_id ON dbo.subscriptions(site_id);
CREATE INDEX idx_subscriptions_plan_id ON dbo.subscriptions(plan_id);
CREATE INDEX idx_subscriptions_status ON dbo.subscriptions(status);
CREATE INDEX idx_subscriptions_stripe_subscription_id ON dbo.subscriptions(stripe_subscription_id);
CREATE INDEX idx_subscriptions_current_period_end ON dbo.subscriptions(current_period_end);
GO

-- ============================================
-- 8. SUBSCRIPTION HISTORY TABLE
-- ============================================
-- Tracks all subscription changes (upgrades, downgrades, cancellations)

CREATE TABLE dbo.subscription_history (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    subscription_id NVARCHAR(50) NOT NULL,
    site_id NVARCHAR(50) NOT NULL,

    -- Change details
    action NVARCHAR(50) NOT NULL,
    -- created, upgraded, downgraded, renewed, canceled, reactivated, paused, resumed,
    -- payment_failed, payment_succeeded, trial_started, trial_ended, coupon_applied

    -- Plan change
    previous_plan_id NVARCHAR(50) NULL,
    new_plan_id NVARCHAR(50) NULL,

    -- Status change
    previous_status NVARCHAR(20) NULL,
    new_status NVARCHAR(20) NULL,

    -- Pricing
    previous_price DECIMAL(10,2) NULL,
    new_price DECIMAL(10,2) NULL,

    -- Additional details (JSON)
    details NVARCHAR(MAX) DEFAULT N'{}',
    /*
    details example:
    {
        "reason": "Upgraded for more agents",
        "proration_amount": 15.50,
        "triggered_by": "user" | "system" | "admin",
        "admin_user_id": "user_xxx",
        "invoice_id": "inv_xxx"
    }
    */

    -- Actor
    performed_by_type NVARCHAR(20) NULL, -- user, admin, system, webhook
    performed_by_id NVARCHAR(50) NULL,

    created_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_subscription_history_subscription FOREIGN KEY (subscription_id) REFERENCES dbo.subscriptions(id),
    CONSTRAINT FK_subscription_history_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id),
    CONSTRAINT FK_subscription_history_prev_plan FOREIGN KEY (previous_plan_id) REFERENCES dbo.subscription_plans(id),
    CONSTRAINT FK_subscription_history_new_plan FOREIGN KEY (new_plan_id) REFERENCES dbo.subscription_plans(id)
);

CREATE INDEX idx_subscription_history_subscription_id ON dbo.subscription_history(subscription_id);
CREATE INDEX idx_subscription_history_site_id ON dbo.subscription_history(site_id);
CREATE INDEX idx_subscription_history_action ON dbo.subscription_history(action);
CREATE INDEX idx_subscription_history_created_at ON dbo.subscription_history(created_at);
GO

-- ============================================
-- 9. COUPON REDEMPTIONS TABLE
-- ============================================
-- Tracks which sites have redeemed which coupons

CREATE TABLE dbo.coupon_redemptions (
    id INT IDENTITY(1,1) PRIMARY KEY,
    coupon_id NVARCHAR(50) NOT NULL,
    site_id NVARCHAR(50) NOT NULL,
    subscription_id NVARCHAR(50) NOT NULL,

    -- Discount applied
    discount_amount DECIMAL(10,2) NOT NULL, -- Actual discount amount
    currency NVARCHAR(3) DEFAULT 'USD',

    redeemed_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_coupon_redemptions_coupon FOREIGN KEY (coupon_id) REFERENCES dbo.coupons(id),
    CONSTRAINT FK_coupon_redemptions_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id),
    CONSTRAINT FK_coupon_redemptions_subscription FOREIGN KEY (subscription_id) REFERENCES dbo.subscriptions(id)
);

CREATE INDEX idx_coupon_redemptions_coupon_id ON dbo.coupon_redemptions(coupon_id);
CREATE INDEX idx_coupon_redemptions_site_id ON dbo.coupon_redemptions(site_id);
GO

-- ============================================
-- 10. INVOICES TABLE
-- ============================================
-- Generated invoices for subscriptions

CREATE TABLE dbo.invoices (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('inv_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    invoice_number NVARCHAR(50) NOT NULL UNIQUE, -- INV-2024-00001
    site_id NVARCHAR(50) NOT NULL,
    subscription_id NVARCHAR(50) NULL,

    -- Invoice status
    status NVARCHAR(20) NOT NULL DEFAULT 'draft',
    -- draft, open, paid, void, uncollectible

    -- Billing period
    period_start DATETIME2 NULL,
    period_end DATETIME2 NULL,

    -- Amounts
    subtotal DECIMAL(10,2) NOT NULL DEFAULT 0,
    discount_amount DECIMAL(10,2) DEFAULT 0,
    tax_amount DECIMAL(10,2) DEFAULT 0,
    tax_rate DECIMAL(5,2) DEFAULT 0, -- e.g., 8.25 for 8.25%
    total DECIMAL(10,2) NOT NULL DEFAULT 0,
    amount_paid DECIMAL(10,2) DEFAULT 0,
    amount_due DECIMAL(10,2) NOT NULL DEFAULT 0,
    currency NVARCHAR(3) DEFAULT 'USD',

    -- Applied coupon
    coupon_id NVARCHAR(50) NULL,

    -- Billing details snapshot (at time of invoice)
    billing_name NVARCHAR(255) NULL,
    billing_email NVARCHAR(255) NULL,
    billing_address NVARCHAR(MAX) NULL, -- JSON

    -- Dates
    invoice_date DATETIME2 DEFAULT SYSDATETIME(),
    due_date DATETIME2 NULL,
    paid_at DATETIME2 NULL,
    voided_at DATETIME2 NULL,

    -- Payment
    payment_method_id NVARCHAR(50) NULL,
    payment_intent_id NVARCHAR(100) NULL, -- Stripe payment intent

    -- Stripe references
    stripe_invoice_id NVARCHAR(100) NULL,
    stripe_hosted_invoice_url NVARCHAR(500) NULL,
    stripe_invoice_pdf NVARCHAR(500) NULL,

    -- Notes
    notes NVARCHAR(MAX) NULL,
    footer NVARCHAR(MAX) NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_invoices_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id),
    CONSTRAINT FK_invoices_subscription FOREIGN KEY (subscription_id) REFERENCES dbo.subscriptions(id),
    CONSTRAINT FK_invoices_coupon FOREIGN KEY (coupon_id) REFERENCES dbo.coupons(id),
    CONSTRAINT FK_invoices_payment_method FOREIGN KEY (payment_method_id) REFERENCES dbo.payment_methods(id)
);

CREATE INDEX idx_invoices_site_id ON dbo.invoices(site_id);
CREATE INDEX idx_invoices_subscription_id ON dbo.invoices(subscription_id);
CREATE INDEX idx_invoices_status ON dbo.invoices(status);
CREATE INDEX idx_invoices_invoice_number ON dbo.invoices(invoice_number);
CREATE INDEX idx_invoices_stripe_invoice_id ON dbo.invoices(stripe_invoice_id);
CREATE INDEX idx_invoices_invoice_date ON dbo.invoices(invoice_date);
GO

-- ============================================
-- 11. INVOICE ITEMS TABLE
-- ============================================
-- Line items on invoices

CREATE TABLE dbo.invoice_items (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('ii_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    invoice_id NVARCHAR(50) NOT NULL,

    -- Item details
    description NVARCHAR(500) NOT NULL,
    quantity INT DEFAULT 1,
    unit_price DECIMAL(10,2) NOT NULL,
    amount DECIMAL(10,2) NOT NULL, -- quantity * unit_price

    -- For subscription items
    plan_id NVARCHAR(50) NULL,
    period_start DATETIME2 NULL,
    period_end DATETIME2 NULL,

    -- Proration
    is_proration BIT DEFAULT 0,
    proration_details NVARCHAR(MAX) NULL, -- JSON

    -- Discount on this item
    discount_amount DECIMAL(10,2) DEFAULT 0,

    -- Type
    type NVARCHAR(50) DEFAULT 'subscription', -- subscription, addon, overage, one_time, credit

    -- Stripe reference
    stripe_invoice_item_id NVARCHAR(100) NULL,

    created_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_invoice_items_invoice FOREIGN KEY (invoice_id) REFERENCES dbo.invoices(id) ON DELETE CASCADE,
    CONSTRAINT FK_invoice_items_plan FOREIGN KEY (plan_id) REFERENCES dbo.subscription_plans(id)
);

CREATE INDEX idx_invoice_items_invoice_id ON dbo.invoice_items(invoice_id);
CREATE INDEX idx_invoice_items_type ON dbo.invoice_items(type);
GO

-- ============================================
-- 12. PAYMENTS TABLE
-- ============================================
-- Payment transactions

CREATE TABLE dbo.payments (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('pay_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    site_id NVARCHAR(50) NOT NULL,
    invoice_id NVARCHAR(50) NULL,
    subscription_id NVARCHAR(50) NULL,

    -- Payment details
    amount DECIMAL(10,2) NOT NULL,
    currency NVARCHAR(3) DEFAULT 'USD',

    -- Status
    status NVARCHAR(20) NOT NULL DEFAULT 'pending',
    -- pending, processing, succeeded, failed, canceled, refunded, partially_refunded

    -- Payment method used
    payment_method_id NVARCHAR(50) NULL,
    payment_method_type NVARCHAR(20) NULL, -- card, bank_transfer, etc.
    card_brand NVARCHAR(20) NULL,
    card_last_four NVARCHAR(4) NULL,

    -- Failure info
    failure_code NVARCHAR(50) NULL,
    failure_message NVARCHAR(500) NULL,

    -- Refund info
    refunded_amount DECIMAL(10,2) DEFAULT 0,

    -- Stripe references
    stripe_payment_intent_id NVARCHAR(100) NULL,
    stripe_charge_id NVARCHAR(100) NULL,
    stripe_balance_transaction_id NVARCHAR(100) NULL,

    -- Fees
    stripe_fee DECIMAL(10,2) NULL,
    net_amount DECIMAL(10,2) NULL, -- amount - stripe_fee

    -- Receipt
    receipt_url NVARCHAR(500) NULL,
    receipt_number NVARCHAR(50) NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),
    succeeded_at DATETIME2 NULL,
    failed_at DATETIME2 NULL,

    CONSTRAINT FK_payments_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id),
    CONSTRAINT FK_payments_invoice FOREIGN KEY (invoice_id) REFERENCES dbo.invoices(id),
    CONSTRAINT FK_payments_subscription FOREIGN KEY (subscription_id) REFERENCES dbo.subscriptions(id),
    CONSTRAINT FK_payments_payment_method FOREIGN KEY (payment_method_id) REFERENCES dbo.payment_methods(id)
);

CREATE INDEX idx_payments_site_id ON dbo.payments(site_id);
CREATE INDEX idx_payments_invoice_id ON dbo.payments(invoice_id);
CREATE INDEX idx_payments_subscription_id ON dbo.payments(subscription_id);
CREATE INDEX idx_payments_status ON dbo.payments(status);
CREATE INDEX idx_payments_stripe_payment_intent_id ON dbo.payments(stripe_payment_intent_id);
CREATE INDEX idx_payments_created_at ON dbo.payments(created_at);
GO

-- ============================================
-- 13. PAYMENT REFUNDS TABLE
-- ============================================
-- Tracks refunds for payments

CREATE TABLE dbo.payment_refunds (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('ref_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    payment_id NVARCHAR(50) NOT NULL,
    site_id NVARCHAR(50) NOT NULL,

    -- Refund details
    amount DECIMAL(10,2) NOT NULL,
    currency NVARCHAR(3) DEFAULT 'USD',
    reason NVARCHAR(500) NULL,

    -- Status
    status NVARCHAR(20) NOT NULL DEFAULT 'pending',
    -- pending, succeeded, failed, canceled

    -- Stripe reference
    stripe_refund_id NVARCHAR(100) NULL,

    -- Who initiated
    refunded_by_type NVARCHAR(20) NULL, -- admin, system
    refunded_by_id NVARCHAR(50) NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_payment_refunds_payment FOREIGN KEY (payment_id) REFERENCES dbo.payments(id),
    CONSTRAINT FK_payment_refunds_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id)
);

CREATE INDEX idx_payment_refunds_payment_id ON dbo.payment_refunds(payment_id);
CREATE INDEX idx_payment_refunds_site_id ON dbo.payment_refunds(site_id);
CREATE INDEX idx_payment_refunds_status ON dbo.payment_refunds(status);
GO

-- ============================================
-- 14. USAGE RECORDS TABLE
-- ============================================
-- Tracks usage for metered billing and limit enforcement

CREATE TABLE dbo.usage_records (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,
    site_id NVARCHAR(50) NOT NULL,
    subscription_id NVARCHAR(50) NOT NULL,

    -- Usage type
    usage_type NVARCHAR(50) NOT NULL,
    -- conversations, messages, ai_requests, file_uploads, api_calls, agents

    -- Usage data
    quantity INT NOT NULL DEFAULT 1,

    -- Period
    period_start DATETIME2 NOT NULL,
    period_end DATETIME2 NOT NULL,

    -- For idempotency (prevent double counting)
    idempotency_key NVARCHAR(100) NULL,

    -- Related resource
    resource_type NVARCHAR(50) NULL, -- conversation, message, file, etc.
    resource_id NVARCHAR(50) NULL,

    -- Timestamps
    recorded_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_usage_records_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id),
    CONSTRAINT FK_usage_records_subscription FOREIGN KEY (subscription_id) REFERENCES dbo.subscriptions(id)
);

CREATE INDEX idx_usage_records_site_id ON dbo.usage_records(site_id);
CREATE INDEX idx_usage_records_subscription_id ON dbo.usage_records(subscription_id);
CREATE INDEX idx_usage_records_usage_type ON dbo.usage_records(usage_type);
CREATE INDEX idx_usage_records_period ON dbo.usage_records(period_start, period_end);
CREATE INDEX idx_usage_records_recorded_at ON dbo.usage_records(recorded_at);
CREATE UNIQUE INDEX idx_usage_records_idempotency ON dbo.usage_records(idempotency_key) WHERE idempotency_key IS NOT NULL;
GO

-- ============================================
-- 15. USERS TABLE (Support Agents & Admins)
-- ============================================

CREATE TABLE dbo.users (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('user_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    username NVARCHAR(50) NOT NULL UNIQUE,
    email NVARCHAR(255) NOT NULL UNIQUE,
    password_hash NVARCHAR(255) NOT NULL,

    -- Profile
    first_name NVARCHAR(100) NULL,
    last_name NVARCHAR(100) NULL,
    avatar_url NVARCHAR(500) NULL,

    -- Role and status
    role NVARCHAR(20) DEFAULT 'support_agent' CHECK (role IN ('admin', 'support_agent')),
    status NVARCHAR(20) DEFAULT 'offline' CHECK (status IN ('online', 'away', 'offline')),

    -- Settings (JSON)
    notification_preferences NVARCHAR(MAX) DEFAULT N'{
        "email_notifications": true,
        "sound_enabled": true,
        "desktop_notifications": true
    }',

    -- Tracking
    last_seen_at DATETIME2 NULL,
    last_login_at DATETIME2 NULL,
    login_count INT DEFAULT 0,

    -- Account status
    is_active BIT DEFAULT 1,
    email_verified BIT DEFAULT 0,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME()
);

CREATE INDEX idx_users_username ON dbo.users(username);
CREATE INDEX idx_users_email ON dbo.users(email);
CREATE INDEX idx_users_status ON dbo.users(status);
CREATE INDEX idx_users_role ON dbo.users(role);
CREATE INDEX idx_users_is_active ON dbo.users(is_active);
GO

-- ============================================
-- 3. USER_SITES TABLE (Many-to-Many)
-- ============================================
-- Links users to the sites they have access to

CREATE TABLE dbo.user_sites (
    id INT IDENTITY(1,1) PRIMARY KEY,
    user_id NVARCHAR(50) NOT NULL,
    site_id NVARCHAR(50) NOT NULL,

    -- Permissions for this site
    can_view BIT DEFAULT 1,
    can_respond BIT DEFAULT 1,
    can_close_conversations BIT DEFAULT 1,
    can_manage_settings BIT DEFAULT 0,

    -- Timestamps
    assigned_at DATETIME2 DEFAULT SYSDATETIME(),
    assigned_by NVARCHAR(50) NULL,

    CONSTRAINT FK_user_sites_user FOREIGN KEY (user_id) REFERENCES dbo.users(id) ON DELETE CASCADE,
    CONSTRAINT FK_user_sites_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE CASCADE,
    CONSTRAINT FK_user_sites_assigned_by FOREIGN KEY (assigned_by) REFERENCES dbo.users(id),
    CONSTRAINT UQ_user_sites UNIQUE (user_id, site_id)
);

CREATE INDEX idx_user_sites_user_id ON dbo.user_sites(user_id);
CREATE INDEX idx_user_sites_site_id ON dbo.user_sites(site_id);
GO

-- ============================================
-- 4. REFRESH TOKENS TABLE
-- ============================================

CREATE TABLE dbo.refresh_tokens (
    id INT IDENTITY(1,1) PRIMARY KEY,
    user_id NVARCHAR(50) NOT NULL,
    token NVARCHAR(255) NOT NULL UNIQUE,

    -- Token metadata (JSON)
    device_info NVARCHAR(MAX) NULL,
    ip_address NVARCHAR(45) NULL, -- IPv6 max length

    -- Expiration
    expires_at DATETIME2 NOT NULL,
    is_revoked BIT DEFAULT 0,
    revoked_at DATETIME2 NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_refresh_tokens_user FOREIGN KEY (user_id) REFERENCES dbo.users(id) ON DELETE CASCADE
);

CREATE INDEX idx_refresh_tokens_user_id ON dbo.refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_token ON dbo.refresh_tokens(token);
CREATE INDEX idx_refresh_tokens_expires_at ON dbo.refresh_tokens(expires_at);
GO

-- ============================================
-- 5. AGENT SESSIONS TABLE
-- ============================================
-- Tracks active support agent sessions

CREATE TABLE dbo.agent_sessions (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('sess_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    user_id NVARCHAR(50) NOT NULL,
    site_id NVARCHAR(50) NOT NULL,

    -- Session token
    token NVARCHAR(255) NOT NULL UNIQUE,

    -- Connection info
    websocket_id NVARCHAR(100) NULL,
    ip_address NVARCHAR(45) NULL,
    user_agent NVARCHAR(MAX) NULL,

    -- Status
    is_active BIT DEFAULT 1,

    -- Timestamps
    connected_at DATETIME2 DEFAULT SYSDATETIME(),
    last_activity_at DATETIME2 DEFAULT SYSDATETIME(),
    disconnected_at DATETIME2 NULL,

    CONSTRAINT FK_agent_sessions_user FOREIGN KEY (user_id) REFERENCES dbo.users(id) ON DELETE CASCADE,
    CONSTRAINT FK_agent_sessions_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE NO ACTION
);

CREATE INDEX idx_agent_sessions_user_id ON dbo.agent_sessions(user_id);
CREATE INDEX idx_agent_sessions_site_id ON dbo.agent_sessions(site_id);
CREATE INDEX idx_agent_sessions_token ON dbo.agent_sessions(token);
CREATE INDEX idx_agent_sessions_is_active ON dbo.agent_sessions(is_active);
GO

-- ============================================
-- 6. VISITORS TABLE
-- ============================================
-- Stores customer/visitor information

CREATE TABLE dbo.visitors (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('vis_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    visitor_id NVARCHAR(100) NOT NULL, -- Client-generated UUID
    site_id NVARCHAR(50) NOT NULL,

    -- Visitor info
    name NVARCHAR(255) NULL,
    email NVARCHAR(255) NULL,
    phone NVARCHAR(50) NULL,

    -- Metadata (JSON)
    metadata NVARCHAR(MAX) DEFAULT N'{}',
    /*
    metadata example:
    {
        "page_url": "https://example.com/products",
        "referrer": "https://google.com",
        "user_agent": "Mozilla/5.0...",
        "screen_resolution": "1920x1080",
        "language": "en-US",
        "timezone": "America/New_York",
        "custom_data": {}
    }
    */

    -- Location (from IP)
    country NVARCHAR(100) NULL,
    city NVARCHAR(100) NULL,
    ip_address NVARCHAR(45) NULL,

    -- Status
    status NVARCHAR(20) DEFAULT 'offline' CHECK (status IN ('online', 'offline')),

    -- Stats
    total_conversations INT DEFAULT 0,
    total_messages INT DEFAULT 0,

    -- Timestamps
    first_seen_at DATETIME2 DEFAULT SYSDATETIME(),
    last_seen_at DATETIME2 DEFAULT SYSDATETIME(),
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_visitors_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE CASCADE,
    CONSTRAINT UQ_visitors_visitor_site UNIQUE (visitor_id, site_id)
);

CREATE INDEX idx_visitors_visitor_id ON dbo.visitors(visitor_id);
CREATE INDEX idx_visitors_site_id ON dbo.visitors(site_id);
CREATE INDEX idx_visitors_email ON dbo.visitors(email);
CREATE INDEX idx_visitors_status ON dbo.visitors(status);
CREATE INDEX idx_visitors_last_seen_at ON dbo.visitors(last_seen_at);
CREATE INDEX idx_visitors_created_at ON dbo.visitors(created_at);
GO

-- ============================================
-- 7. VISITOR SESSIONS TABLE
-- ============================================
-- Tracks active visitor sessions

CREATE TABLE dbo.visitor_sessions (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('vsess_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    visitor_id NVARCHAR(50) NOT NULL,
    site_id NVARCHAR(50) NOT NULL,

    -- Session token
    token NVARCHAR(255) NOT NULL UNIQUE,

    -- Connection info
    websocket_id NVARCHAR(100) NULL,
    current_page_url NVARCHAR(MAX) NULL,
    ip_address NVARCHAR(45) NULL,
    user_agent NVARCHAR(MAX) NULL,

    -- Status
    is_active BIT DEFAULT 1,

    -- Timestamps
    connected_at DATETIME2 DEFAULT SYSDATETIME(),
    last_activity_at DATETIME2 DEFAULT SYSDATETIME(),
    disconnected_at DATETIME2 NULL,

    CONSTRAINT FK_visitor_sessions_visitor FOREIGN KEY (visitor_id) REFERENCES dbo.visitors(id) ON DELETE CASCADE,
    CONSTRAINT FK_visitor_sessions_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE NO ACTION
);

CREATE INDEX idx_visitor_sessions_visitor_id ON dbo.visitor_sessions(visitor_id);
CREATE INDEX idx_visitor_sessions_site_id ON dbo.visitor_sessions(site_id);
CREATE INDEX idx_visitor_sessions_token ON dbo.visitor_sessions(token);
CREATE INDEX idx_visitor_sessions_is_active ON dbo.visitor_sessions(is_active);
GO

-- ============================================
-- 8. CONVERSATIONS TABLE
-- ============================================

CREATE TABLE dbo.conversations (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('conv_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    site_id NVARCHAR(50) NOT NULL,
    visitor_id NVARCHAR(50) NOT NULL,

    -- Assignment
    assigned_agent_id NVARCHAR(50) NULL,
    assigned_at DATETIME2 NULL,

    -- Status
    status NVARCHAR(20) DEFAULT 'active' CHECK (status IN ('active', 'pending', 'closed')),
    priority INT DEFAULT 0, -- 0=normal, 1=high, 2=urgent

    -- Closure info
    closed_by NVARCHAR(50) NULL,
    closed_at DATETIME2 NULL,
    resolution_notes NVARCHAR(MAX) NULL,

    -- Stats
    message_count INT DEFAULT 0,
    visitor_message_count INT DEFAULT 0,
    agent_message_count INT DEFAULT 0,

    -- Response metrics
    first_response_at DATETIME2 NULL,
    first_response_time_seconds INT NULL, -- Time to first response

    -- Rating
    rating INT NULL CHECK (rating >= 1 AND rating <= 5),
    rating_feedback NVARCHAR(MAX) NULL,
    rated_at DATETIME2 NULL,

    -- Tags/Labels (comma-separated for SQL Server)
    tags NVARCHAR(MAX) DEFAULT '',

    -- Timestamps
    last_message_at DATETIME2 NULL,
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_conversations_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE CASCADE,
    CONSTRAINT FK_conversations_visitor FOREIGN KEY (visitor_id) REFERENCES dbo.visitors(id) ON DELETE NO ACTION,
    CONSTRAINT FK_conversations_agent FOREIGN KEY (assigned_agent_id) REFERENCES dbo.users(id) ON DELETE SET NULL,
    CONSTRAINT FK_conversations_closed_by FOREIGN KEY (closed_by) REFERENCES dbo.users(id)
);

CREATE INDEX idx_conversations_site_id ON dbo.conversations(site_id);
CREATE INDEX idx_conversations_visitor_id ON dbo.conversations(visitor_id);
CREATE INDEX idx_conversations_assigned_agent_id ON dbo.conversations(assigned_agent_id);
CREATE INDEX idx_conversations_status ON dbo.conversations(status);
CREATE INDEX idx_conversations_created_at ON dbo.conversations(created_at);
CREATE INDEX idx_conversations_last_message_at ON dbo.conversations(last_message_at);
GO

-- ============================================
-- 9. FILES TABLE
-- ============================================

CREATE TABLE dbo.files (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('file_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    site_id NVARCHAR(50) NOT NULL,

    -- File info
    filename NVARCHAR(255) NOT NULL, -- Stored filename (UUID-based)
    original_name NVARCHAR(255) NOT NULL, -- Original upload name
    mime_type NVARCHAR(100) NOT NULL,
    size_bytes BIGINT NOT NULL,

    -- Storage
    storage_path NVARCHAR(MAX) NOT NULL, -- Full path or S3 key
    storage_provider NVARCHAR(50) DEFAULT 'local', -- local, s3, azure, etc.

    -- URLs
    url NVARCHAR(MAX) NOT NULL,
    thumbnail_url NVARCHAR(MAX) NULL, -- For images

    -- Type flags
    is_image BIT DEFAULT 0,

    -- Image metadata (if applicable)
    image_width INT NULL,
    image_height INT NULL,

    -- Uploader info
    uploaded_by_type NVARCHAR(20) NOT NULL CHECK (uploaded_by_type IN ('visitor', 'support', 'system')),
    uploaded_by_id NVARCHAR(50) NOT NULL, -- visitor_id or user_id

    -- Association
    conversation_id NVARCHAR(50) NULL,
    message_id NVARCHAR(50) NULL, -- Will be set after message creation

    -- Virus scan
    scan_status NVARCHAR(20) DEFAULT 'pending', -- pending, clean, infected
    scanned_at DATETIME2 NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    deleted_at DATETIME2 NULL, -- Soft delete

    CONSTRAINT FK_files_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE CASCADE,
    CONSTRAINT FK_files_conversation FOREIGN KEY (conversation_id) REFERENCES dbo.conversations(id) ON DELETE SET NULL
);

CREATE INDEX idx_files_site_id ON dbo.files(site_id);
CREATE INDEX idx_files_conversation_id ON dbo.files(conversation_id);
CREATE INDEX idx_files_uploaded_by ON dbo.files(uploaded_by_type, uploaded_by_id);
CREATE INDEX idx_files_created_at ON dbo.files(created_at);
CREATE INDEX idx_files_mime_type ON dbo.files(mime_type);
GO

-- ============================================
-- 10. MESSAGES TABLE
-- ============================================

CREATE TABLE dbo.messages (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('msg_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    conversation_id NVARCHAR(50) NOT NULL,

    -- Sender info
    sender_type NVARCHAR(20) NOT NULL CHECK (sender_type IN ('visitor', 'support', 'system')),
    sender_id NVARCHAR(50) NOT NULL, -- visitor_id or user_id
    sender_name NVARCHAR(255) NULL, -- Denormalized for quick access

    -- Content
    content NVARCHAR(MAX) NULL,
    message_type NVARCHAR(20) DEFAULT 'text' CHECK (message_type IN ('text', 'file', 'image', 'system')),

    -- File attachment
    file_id NVARCHAR(50) NULL,

    -- Status
    status NVARCHAR(20) DEFAULT 'sent' CHECK (status IN ('sent', 'delivered', 'read')),

    -- Metadata (JSON)
    metadata NVARCHAR(MAX) DEFAULT N'{}',
    /*
    metadata example:
    {
        "client_message_id": "uuid",
        "edited": false,
        "edited_at": null,
        "reply_to_message_id": null
    }
    */

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_messages_conversation FOREIGN KEY (conversation_id) REFERENCES dbo.conversations(id) ON DELETE CASCADE,
    CONSTRAINT FK_messages_file FOREIGN KEY (file_id) REFERENCES dbo.files(id) ON DELETE SET NULL
);

CREATE INDEX idx_messages_conversation_id ON dbo.messages(conversation_id);
CREATE INDEX idx_messages_sender ON dbo.messages(sender_type, sender_id);
CREATE INDEX idx_messages_created_at ON dbo.messages(created_at);
CREATE INDEX idx_messages_message_type ON dbo.messages(message_type);
CREATE INDEX idx_messages_file_id ON dbo.messages(file_id);

-- Composite index for fetching conversation messages
CREATE INDEX idx_messages_conv_created ON dbo.messages(conversation_id, created_at DESC);
GO

-- ============================================
-- 11. MESSAGE READS TABLE
-- ============================================
-- Tracks which messages have been read by whom

CREATE TABLE dbo.message_reads (
    id INT IDENTITY(1,1) PRIMARY KEY,
    message_id NVARCHAR(50) NOT NULL,

    -- Reader info
    reader_type NVARCHAR(20) NOT NULL CHECK (reader_type IN ('visitor', 'support', 'system')),
    reader_id NVARCHAR(50) NOT NULL,

    -- Timestamps
    read_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_message_reads_message FOREIGN KEY (message_id) REFERENCES dbo.messages(id) ON DELETE CASCADE,
    CONSTRAINT UQ_message_reads UNIQUE (message_id, reader_type, reader_id)
);

CREATE INDEX idx_message_reads_message_id ON dbo.message_reads(message_id);
CREATE INDEX idx_message_reads_reader ON dbo.message_reads(reader_type, reader_id);
GO

-- ============================================
-- 12. CONVERSATION ANALYSIS TABLE
-- ============================================
-- Stores AI analysis results for conversations

CREATE TABLE dbo.conversation_analysis (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('analysis_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    conversation_id NVARCHAR(50) NOT NULL,
    message_id NVARCHAR(50) NULL, -- If analysis is per-message

    -- Analysis results
    suggested_reply NVARCHAR(MAX) NULL,
    interest_level NVARCHAR(20) NULL, -- Low, Medium, High
    conversion_percentage INT NULL CHECK (conversion_percentage >= 0 AND conversion_percentage <= 100),
    objection NVARCHAR(MAX) NULL,
    next_action NVARCHAR(MAX) NULL,

    -- Additional analysis
    sentiment NVARCHAR(20) NULL, -- positive, negative, neutral
    intent NVARCHAR(100) NULL, -- purchase_inquiry, support_request, complaint, etc.
    keywords NVARCHAR(MAX) NULL, -- Comma-separated
    summary NVARCHAR(MAX) NULL,

    -- AI model info
    ai_model NVARCHAR(50) NULL,
    ai_tokens_used INT NULL,
    ai_processing_time_ms INT NULL,

    -- Raw response (JSON)
    raw_response NVARCHAR(MAX) NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_conversation_analysis_conversation FOREIGN KEY (conversation_id) REFERENCES dbo.conversations(id) ON DELETE CASCADE,
    CONSTRAINT FK_conversation_analysis_message FOREIGN KEY (message_id) REFERENCES dbo.messages(id) ON DELETE NO ACTION
);

CREATE INDEX idx_conversation_analysis_conversation_id ON dbo.conversation_analysis(conversation_id);
CREATE INDEX idx_conversation_analysis_message_id ON dbo.conversation_analysis(message_id);
CREATE INDEX idx_conversation_analysis_created_at ON dbo.conversation_analysis(created_at);
GO

-- ============================================
-- 13. NOTIFICATIONS TABLE
-- ============================================

CREATE TABLE dbo.notifications (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('notif_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    user_id NVARCHAR(50) NOT NULL,
    site_id NVARCHAR(50) NULL,

    -- Notification content
    type NVARCHAR(50) NOT NULL CHECK (type IN ('new_conversation', 'new_message', 'conversation_assigned', 'visitor_waiting', 'system')),
    title NVARCHAR(255) NOT NULL,
    message NVARCHAR(MAX) NOT NULL,

    -- Related data (JSON)
    data NVARCHAR(MAX) DEFAULT N'{}',
    /*
    data example:
    {
        "conversation_id": "conv_123",
        "visitor_id": "vis_456",
        "visitor_name": "John Doe",
        "message_preview": "Hello, I need help..."
    }
    */

    -- Action URL
    action_url NVARCHAR(MAX) NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    expires_at DATETIME2 NULL,

    CONSTRAINT FK_notifications_user FOREIGN KEY (user_id) REFERENCES dbo.users(id) ON DELETE CASCADE,
    CONSTRAINT FK_notifications_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE SET NULL
);

CREATE INDEX idx_notifications_user_id ON dbo.notifications(user_id);
CREATE INDEX idx_notifications_site_id ON dbo.notifications(site_id);
CREATE INDEX idx_notifications_type ON dbo.notifications(type);
CREATE INDEX idx_notifications_created_at ON dbo.notifications(created_at);
GO

-- ============================================
-- 14. NOTIFICATION READS TABLE
-- ============================================

CREATE TABLE dbo.notification_reads (
    id INT IDENTITY(1,1) PRIMARY KEY,
    notification_id NVARCHAR(50) NOT NULL,
    user_id NVARCHAR(50) NOT NULL,

    read_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_notification_reads_notification FOREIGN KEY (notification_id) REFERENCES dbo.notifications(id) ON DELETE CASCADE,
    CONSTRAINT FK_notification_reads_user FOREIGN KEY (user_id) REFERENCES dbo.users(id) ON DELETE NO ACTION,
    CONSTRAINT UQ_notification_reads UNIQUE (notification_id, user_id)
);

CREATE INDEX idx_notification_reads_user_id ON dbo.notification_reads(user_id);
CREATE INDEX idx_notification_reads_notification_id ON dbo.notification_reads(notification_id);
GO

-- ============================================
-- 15. TYPING INDICATORS TABLE
-- ============================================
-- For persistent typing state (useful for reconnections)

CREATE TABLE dbo.typing_indicators (
    id INT IDENTITY(1,1) PRIMARY KEY,
    conversation_id NVARCHAR(50) NOT NULL,

    -- Typer info
    typer_type NVARCHAR(20) NOT NULL CHECK (typer_type IN ('visitor', 'support', 'system')),
    typer_id NVARCHAR(50) NOT NULL,

    -- Status
    is_typing BIT DEFAULT 1,

    -- Timestamps
    started_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_typing_indicators_conversation FOREIGN KEY (conversation_id) REFERENCES dbo.conversations(id) ON DELETE CASCADE,
    CONSTRAINT UQ_typing_indicators UNIQUE (conversation_id, typer_type, typer_id)
);

CREATE INDEX idx_typing_indicators_conversation_id ON dbo.typing_indicators(conversation_id);
GO

-- ============================================
-- 16. CANNED RESPONSES TABLE
-- ============================================
-- Pre-written responses for agents

CREATE TABLE dbo.canned_responses (
    id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('canned_' + LOWER(SUBSTRING(CONVERT(NVARCHAR(36), NEWID()), 1, 12))),
    site_id NVARCHAR(50) NULL, -- NULL = global
    user_id NVARCHAR(50) NULL, -- NULL = shared

    -- Response content
    title NVARCHAR(255) NOT NULL,
    content NVARCHAR(MAX) NOT NULL,
    shortcut NVARCHAR(50) NULL, -- e.g., "/greeting" or "#hello"

    -- Categorization
    category NVARCHAR(100) NULL,
    tags NVARCHAR(MAX) DEFAULT '', -- Comma-separated

    -- Usage stats
    use_count INT DEFAULT 0,
    last_used_at DATETIME2 NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_canned_responses_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE CASCADE,
    CONSTRAINT FK_canned_responses_user FOREIGN KEY (user_id) REFERENCES dbo.users(id) ON DELETE SET NULL
);

CREATE INDEX idx_canned_responses_site_id ON dbo.canned_responses(site_id);
CREATE INDEX idx_canned_responses_user_id ON dbo.canned_responses(user_id);
CREATE INDEX idx_canned_responses_shortcut ON dbo.canned_responses(shortcut);
CREATE INDEX idx_canned_responses_category ON dbo.canned_responses(category);
GO

-- ============================================
-- 17. AUDIT LOG TABLE
-- ============================================
-- Tracks important actions for compliance/debugging

CREATE TABLE dbo.audit_logs (
    id BIGINT IDENTITY(1,1) PRIMARY KEY,

    -- Actor info
    actor_type NVARCHAR(20) NOT NULL, -- user, visitor, system
    actor_id NVARCHAR(50) NULL,

    -- Action info
    action NVARCHAR(100) NOT NULL, -- login, logout, message_sent, file_uploaded, etc.
    resource_type NVARCHAR(50) NULL, -- conversation, message, file, user, etc.
    resource_id NVARCHAR(50) NULL,

    -- Details (JSON)
    details NVARCHAR(MAX) DEFAULT N'{}',

    -- Request info
    ip_address NVARCHAR(45) NULL,
    user_agent NVARCHAR(MAX) NULL,

    -- Site context
    site_id NVARCHAR(50) NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_audit_logs_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE SET NULL
);

CREATE INDEX idx_audit_logs_actor ON dbo.audit_logs(actor_type, actor_id);
CREATE INDEX idx_audit_logs_action ON dbo.audit_logs(action);
CREATE INDEX idx_audit_logs_resource ON dbo.audit_logs(resource_type, resource_id);
CREATE INDEX idx_audit_logs_site_id ON dbo.audit_logs(site_id);
CREATE INDEX idx_audit_logs_created_at ON dbo.audit_logs(created_at);
GO

-- ============================================
-- 18. ANALYTICS DAILY STATS TABLE
-- ============================================
-- Pre-aggregated daily statistics

CREATE TABLE dbo.analytics_daily (
    id INT IDENTITY(1,1) PRIMARY KEY,
    site_id NVARCHAR(50) NOT NULL,
    date DATE NOT NULL,

    -- Conversation metrics
    total_conversations INT DEFAULT 0,
    new_conversations INT DEFAULT 0,
    closed_conversations INT DEFAULT 0,

    -- Message metrics
    total_messages INT DEFAULT 0,
    visitor_messages INT DEFAULT 0,
    agent_messages INT DEFAULT 0,

    -- Response metrics
    avg_first_response_time_seconds INT NULL,
    avg_resolution_time_seconds INT NULL,

    -- Visitor metrics
    unique_visitors INT DEFAULT 0,
    returning_visitors INT DEFAULT 0,

    -- Satisfaction metrics
    total_ratings INT DEFAULT 0,
    avg_rating DECIMAL(3,2) NULL,

    -- Agent metrics
    active_agents INT DEFAULT 0,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_analytics_daily_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE CASCADE,
    CONSTRAINT UQ_analytics_daily UNIQUE (site_id, date)
);

CREATE INDEX idx_analytics_daily_site_id ON dbo.analytics_daily(site_id);
CREATE INDEX idx_analytics_daily_date ON dbo.analytics_daily(date);
GO

-- ============================================
-- 19. AGENT ANALYTICS TABLE
-- ============================================
-- Per-agent daily statistics

CREATE TABLE dbo.agent_analytics_daily (
    id INT IDENTITY(1,1) PRIMARY KEY,
    user_id NVARCHAR(50) NOT NULL,
    site_id NVARCHAR(50) NOT NULL,
    date DATE NOT NULL,

    -- Conversation metrics
    conversations_handled INT DEFAULT 0,
    conversations_closed INT DEFAULT 0,

    -- Message metrics
    messages_sent INT DEFAULT 0,

    -- Response metrics
    avg_response_time_seconds INT NULL,
    avg_resolution_time_seconds INT NULL,

    -- Activity metrics
    online_duration_seconds INT DEFAULT 0,

    -- Satisfaction
    ratings_received INT DEFAULT 0,
    avg_rating DECIMAL(3,2) NULL,

    -- Timestamps
    created_at DATETIME2 DEFAULT SYSDATETIME(),
    updated_at DATETIME2 DEFAULT SYSDATETIME(),

    CONSTRAINT FK_agent_analytics_user FOREIGN KEY (user_id) REFERENCES dbo.users(id) ON DELETE CASCADE,
    CONSTRAINT FK_agent_analytics_site FOREIGN KEY (site_id) REFERENCES dbo.sites(id) ON DELETE NO ACTION,
    CONSTRAINT UQ_agent_analytics_daily UNIQUE (user_id, site_id, date)
);

CREATE INDEX idx_agent_analytics_user_id ON dbo.agent_analytics_daily(user_id);
CREATE INDEX idx_agent_analytics_site_id ON dbo.agent_analytics_daily(site_id);
CREATE INDEX idx_agent_analytics_date ON dbo.agent_analytics_daily(date);
GO

-- ============================================
-- VIEWS
-- ============================================

-- View: Active conversations with latest message
CREATE OR ALTER VIEW dbo.v_active_conversations AS
SELECT
    c.id,
    c.site_id,
    c.visitor_id,
    v.name AS visitor_name,
    v.email AS visitor_email,
    c.assigned_agent_id,
    u.username AS agent_username,
    c.status,
    c.message_count,
    c.last_message_at,
    c.created_at,
    (
        SELECT TOP 1 content
        FROM dbo.messages m
        WHERE m.conversation_id = c.id
        ORDER BY m.created_at DESC
    ) AS last_message_content,
    (
        SELECT TOP 1 sender_type
        FROM dbo.messages m
        WHERE m.conversation_id = c.id
        ORDER BY m.created_at DESC
    ) AS last_message_sender
FROM dbo.conversations c
INNER JOIN dbo.visitors v ON c.visitor_id = v.id
LEFT JOIN dbo.users u ON c.assigned_agent_id = u.id
WHERE c.status = 'active';
GO

-- View: Unread message counts per conversation per agent
CREATE OR ALTER VIEW dbo.v_unread_counts AS
SELECT
    c.id AS conversation_id,
    c.assigned_agent_id,
    COUNT(m.id) AS unread_count
FROM dbo.conversations c
INNER JOIN dbo.messages m ON m.conversation_id = c.id
LEFT JOIN dbo.message_reads mr ON mr.message_id = m.id
    AND mr.reader_type = 'support'
    AND mr.reader_id = c.assigned_agent_id
WHERE m.sender_type = 'visitor'
    AND mr.id IS NULL
    AND c.status = 'active'
GROUP BY c.id, c.assigned_agent_id;
GO

-- View: Online agents per site
CREATE OR ALTER VIEW dbo.v_online_agents AS
SELECT
    us.site_id,
    u.id AS user_id,
    u.username,
    u.first_name,
    u.last_name,
    u.status,
    u.last_seen_at,
    (
        SELECT COUNT(*)
        FROM dbo.conversations c
        WHERE c.assigned_agent_id = u.id
        AND c.status = 'active'
    ) AS active_conversations
FROM dbo.users u
INNER JOIN dbo.user_sites us ON u.id = us.user_id
WHERE u.status IN ('online', 'away')
AND u.is_active = 1;
GO

-- View: Site subscription details
CREATE OR ALTER VIEW dbo.v_site_subscriptions AS
SELECT
    s.id AS site_id,
    s.name AS site_name,
    s.domain,
    s.stripe_customer_id,
    sub.id AS subscription_id,
    sub.status AS subscription_status,
    sub.billing_cycle,
    sub.current_period_start,
    sub.current_period_end,
    sub.is_trial,
    sub.trial_end,
    sub.cancel_at_period_end,
    p.id AS plan_id,
    p.name AS plan_name,
    p.code AS plan_code,
    p.price_monthly,
    p.price_yearly,
    p.max_agents,
    p.max_conversations_per_month,
    p.max_sites,
    sub.conversations_used_this_period,
    sub.messages_used_this_period,
    sub.storage_used_mb,
    CASE
        WHEN p.max_conversations_per_month IS NOT NULL
        THEN p.max_conversations_per_month - sub.conversations_used_this_period
        ELSE NULL
    END AS conversations_remaining
FROM dbo.sites s
LEFT JOIN dbo.subscriptions sub ON s.id = sub.site_id AND sub.status IN ('active', 'trialing', 'past_due')
LEFT JOIN dbo.subscription_plans p ON sub.plan_id = p.id;
GO

-- View: Monthly revenue by plan
CREATE OR ALTER VIEW dbo.v_monthly_revenue AS
SELECT
    p.id AS plan_id,
    p.name AS plan_name,
    p.code AS plan_code,
    COUNT(DISTINCT sub.id) AS total_subscriptions,
    SUM(CASE WHEN sub.billing_cycle = 'monthly' THEN 1 ELSE 0 END) AS monthly_subscriptions,
    SUM(CASE WHEN sub.billing_cycle = 'yearly' THEN 1 ELSE 0 END) AS yearly_subscriptions,
    SUM(CASE WHEN sub.billing_cycle = 'monthly' THEN sub.price_at_subscription ELSE 0 END) AS monthly_mrr,
    SUM(CASE WHEN sub.billing_cycle = 'yearly' THEN sub.price_at_subscription / 12 ELSE 0 END) AS yearly_mrr,
    SUM(CASE
        WHEN sub.billing_cycle = 'monthly' THEN sub.price_at_subscription
        ELSE sub.price_at_subscription / 12
    END) AS total_mrr
FROM dbo.subscription_plans p
LEFT JOIN dbo.subscriptions sub ON p.id = sub.plan_id AND sub.status = 'active'
GROUP BY p.id, p.name, p.code;
GO

-- View: Invoice summary
CREATE OR ALTER VIEW dbo.v_invoice_summary AS
SELECT
    i.id,
    i.invoice_number,
    i.site_id,
    s.name AS site_name,
    i.status,
    i.subtotal,
    i.discount_amount,
    i.tax_amount,
    i.total,
    i.amount_paid,
    i.amount_due,
    i.currency,
    i.invoice_date,
    i.due_date,
    i.paid_at,
    p.name AS plan_name,
    sub.billing_cycle
FROM dbo.invoices i
INNER JOIN dbo.sites s ON i.site_id = s.id
LEFT JOIN dbo.subscriptions sub ON i.subscription_id = sub.id
LEFT JOIN dbo.subscription_plans p ON sub.plan_id = p.id;
GO

-- ============================================
-- TRIGGERS
-- ============================================

-- Trigger: Update updated_at on sites
CREATE OR ALTER TRIGGER trg_sites_updated_at
ON dbo.sites
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.sites
    SET updated_at = SYSDATETIME()
    FROM dbo.sites s
    INNER JOIN inserted i ON s.id = i.id;
END;
GO

-- Trigger: Update updated_at on users
CREATE OR ALTER TRIGGER trg_users_updated_at
ON dbo.users
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.users
    SET updated_at = SYSDATETIME()
    FROM dbo.users u
    INNER JOIN inserted i ON u.id = i.id;
END;
GO

-- Trigger: Update updated_at on visitors
CREATE OR ALTER TRIGGER trg_visitors_updated_at
ON dbo.visitors
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.visitors
    SET updated_at = SYSDATETIME()
    FROM dbo.visitors v
    INNER JOIN inserted i ON v.id = i.id;
END;
GO

-- Trigger: Update updated_at on conversations
CREATE OR ALTER TRIGGER trg_conversations_updated_at
ON dbo.conversations
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.conversations
    SET updated_at = SYSDATETIME()
    FROM dbo.conversations c
    INNER JOIN inserted i ON c.id = i.id;
END;
GO

-- Trigger: Update updated_at on messages
CREATE OR ALTER TRIGGER trg_messages_updated_at
ON dbo.messages
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.messages
    SET updated_at = SYSDATETIME()
    FROM dbo.messages m
    INNER JOIN inserted i ON m.id = i.id;
END;
GO

-- Trigger: Increment conversation message count on new message
CREATE OR ALTER TRIGGER trg_messages_increment_count
ON dbo.messages
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE c
    SET
        message_count = c.message_count + 1,
        visitor_message_count = CASE WHEN i.sender_type = 'visitor' THEN c.visitor_message_count + 1 ELSE c.visitor_message_count END,
        agent_message_count = CASE WHEN i.sender_type = 'support' THEN c.agent_message_count + 1 ELSE c.agent_message_count END,
        last_message_at = i.created_at,
        first_response_at = CASE
            WHEN c.first_response_at IS NULL AND i.sender_type = 'support' THEN i.created_at
            ELSE c.first_response_at
        END,
        first_response_time_seconds = CASE
            WHEN c.first_response_at IS NULL AND i.sender_type = 'support'
            THEN DATEDIFF(SECOND, c.created_at, i.created_at)
            ELSE c.first_response_time_seconds
        END
    FROM dbo.conversations c
    INNER JOIN inserted i ON c.id = i.conversation_id;
END;
GO

-- Trigger: Update visitor stats on new message
CREATE OR ALTER TRIGGER trg_messages_update_visitor_stats
ON dbo.messages
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE v
    SET
        total_messages = v.total_messages + 1,
        last_seen_at = i.created_at
    FROM dbo.visitors v
    INNER JOIN inserted i ON v.id = i.sender_id
    WHERE i.sender_type = 'visitor';
END;
GO

-- Trigger: Increment visitor conversation count on new conversation
CREATE OR ALTER TRIGGER trg_conversations_increment_visitor_count
ON dbo.conversations
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE v
    SET total_conversations = v.total_conversations + 1
    FROM dbo.visitors v
    INNER JOIN inserted i ON v.id = i.visitor_id;
END;
GO

-- ============================================
-- STORED PROCEDURES
-- ============================================

-- Procedure: Get conversation with messages
CREATE OR ALTER PROCEDURE dbo.sp_GetConversationWithMessages
    @conversation_id NVARCHAR(50),
    @messages_limit INT = 50,
    @before_message_id NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Get conversation details
    SELECT
        c.*,
        v.name AS visitor_name,
        v.email AS visitor_email,
        u.username AS agent_username
    FROM dbo.conversations c
    INNER JOIN dbo.visitors v ON c.visitor_id = v.id
    LEFT JOIN dbo.users u ON c.assigned_agent_id = u.id
    WHERE c.id = @conversation_id;

    -- Get messages
    IF @before_message_id IS NOT NULL
    BEGIN
        SELECT TOP (@messages_limit) m.*, f.original_name AS file_name, f.url AS file_url, f.is_image
        FROM dbo.messages m
        LEFT JOIN dbo.files f ON m.file_id = f.id
        WHERE m.conversation_id = @conversation_id
        AND m.created_at < (SELECT created_at FROM dbo.messages WHERE id = @before_message_id)
        ORDER BY m.created_at DESC;
    END
    ELSE
    BEGIN
        SELECT TOP (@messages_limit) m.*, f.original_name AS file_name, f.url AS file_url, f.is_image
        FROM dbo.messages m
        LEFT JOIN dbo.files f ON m.file_id = f.id
        WHERE m.conversation_id = @conversation_id
        ORDER BY m.created_at DESC;
    END
END;
GO

-- Procedure: Get dashboard statistics
CREATE OR ALTER PROCEDURE dbo.sp_GetDashboardStats
    @site_id NVARCHAR(50),
    @start_date DATE,
    @end_date DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Total conversations
    SELECT
        COUNT(*) AS total_conversations,
        SUM(CASE WHEN status = 'active' THEN 1 ELSE 0 END) AS active_conversations,
        SUM(CASE WHEN status = 'closed' THEN 1 ELSE 0 END) AS closed_conversations,
        AVG(first_response_time_seconds) AS avg_first_response_time,
        AVG(rating) AS avg_rating
    FROM dbo.conversations
    WHERE site_id = @site_id
    AND CAST(created_at AS DATE) BETWEEN @start_date AND @end_date;

    -- Total messages
    SELECT
        COUNT(*) AS total_messages,
        SUM(CASE WHEN sender_type = 'visitor' THEN 1 ELSE 0 END) AS visitor_messages,
        SUM(CASE WHEN sender_type = 'support' THEN 1 ELSE 0 END) AS agent_messages
    FROM dbo.messages m
    INNER JOIN dbo.conversations c ON m.conversation_id = c.id
    WHERE c.site_id = @site_id
    AND CAST(m.created_at AS DATE) BETWEEN @start_date AND @end_date;

    -- Unique visitors
    SELECT COUNT(DISTINCT visitor_id) AS unique_visitors
    FROM dbo.conversations
    WHERE site_id = @site_id
    AND CAST(created_at AS DATE) BETWEEN @start_date AND @end_date;
END;
GO

-- Procedure: Mark messages as read
CREATE OR ALTER PROCEDURE dbo.sp_MarkMessagesAsRead
    @conversation_id NVARCHAR(50),
    @reader_type NVARCHAR(20),
    @reader_id NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.message_reads (message_id, reader_type, reader_id)
    SELECT m.id, @reader_type, @reader_id
    FROM dbo.messages m
    LEFT JOIN dbo.message_reads mr ON m.id = mr.message_id
        AND mr.reader_type = @reader_type
        AND mr.reader_id = @reader_id
    WHERE m.conversation_id = @conversation_id
    AND mr.id IS NULL
    AND m.sender_type != @reader_type;

    SELECT @@ROWCOUNT AS marked_count;
END;
GO

-- ============================================
-- BILLING STORED PROCEDURES
-- ============================================

-- Procedure: Check if site can perform action based on subscription limits
CREATE OR ALTER PROCEDURE dbo.sp_CheckSubscriptionLimit
    @site_id NVARCHAR(50),
    @limit_type NVARCHAR(50), -- conversations, messages, agents, storage
    @quantity INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @can_proceed BIT = 0;
    DECLARE @current_usage INT = 0;
    DECLARE @limit_value INT = NULL;
    DECLARE @plan_name NVARCHAR(100);
    DECLARE @subscription_status NVARCHAR(20);

    -- Get subscription info
    SELECT
        @subscription_status = sub.status,
        @plan_name = p.name,
        @limit_value = CASE @limit_type
            WHEN 'conversations' THEN p.max_conversations_per_month
            WHEN 'agents' THEN p.max_agents
            WHEN 'storage' THEN p.max_file_storage_mb
            ELSE NULL
        END,
        @current_usage = CASE @limit_type
            WHEN 'conversations' THEN sub.conversations_used_this_period
            WHEN 'storage' THEN CAST(sub.storage_used_mb AS INT)
            ELSE 0
        END
    FROM dbo.subscriptions sub
    INNER JOIN dbo.subscription_plans p ON sub.plan_id = p.id
    WHERE sub.site_id = @site_id
    AND sub.status IN ('active', 'trialing');

    -- Check if within limits
    IF @subscription_status IS NULL
    BEGIN
        -- No active subscription
        SELECT
            @can_proceed AS can_proceed,
            'No active subscription' AS message,
            NULL AS current_usage,
            NULL AS limit_value,
            NULL AS plan_name;
        RETURN;
    END

    IF @limit_value IS NULL
    BEGIN
        -- Unlimited
        SET @can_proceed = 1;
    END
    ELSE IF (@current_usage + @quantity) <= @limit_value
    BEGIN
        SET @can_proceed = 1;
    END

    SELECT
        @can_proceed AS can_proceed,
        CASE
            WHEN @can_proceed = 1 THEN 'OK'
            ELSE 'Limit exceeded for ' + @limit_type
        END AS message,
        @current_usage AS current_usage,
        @limit_value AS limit_value,
        @plan_name AS plan_name;
END;
GO

-- Procedure: Record usage
CREATE OR ALTER PROCEDURE dbo.sp_RecordUsage
    @site_id NVARCHAR(50),
    @usage_type NVARCHAR(50),
    @quantity INT = 1,
    @resource_type NVARCHAR(50) = NULL,
    @resource_id NVARCHAR(50) = NULL,
    @idempotency_key NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @subscription_id NVARCHAR(50);
    DECLARE @period_start DATETIME2;
    DECLARE @period_end DATETIME2;

    -- Get active subscription
    SELECT
        @subscription_id = id,
        @period_start = current_period_start,
        @period_end = current_period_end
    FROM dbo.subscriptions
    WHERE site_id = @site_id
    AND status IN ('active', 'trialing');

    IF @subscription_id IS NULL
    BEGIN
        RAISERROR('No active subscription found', 16, 1);
        RETURN;
    END

    -- Check idempotency
    IF @idempotency_key IS NOT NULL AND EXISTS (
        SELECT 1 FROM dbo.usage_records WHERE idempotency_key = @idempotency_key
    )
    BEGIN
        -- Already recorded
        SELECT 'Already recorded' AS status;
        RETURN;
    END

    -- Insert usage record
    INSERT INTO dbo.usage_records (
        site_id, subscription_id, usage_type, quantity,
        period_start, period_end, resource_type, resource_id, idempotency_key
    )
    VALUES (
        @site_id, @subscription_id, @usage_type, @quantity,
        @period_start, @period_end, @resource_type, @resource_id, @idempotency_key
    );

    -- Update subscription counters
    IF @usage_type = 'conversations'
    BEGIN
        UPDATE dbo.subscriptions
        SET conversations_used_this_period = conversations_used_this_period + @quantity
        WHERE id = @subscription_id;
    END
    ELSE IF @usage_type = 'messages'
    BEGIN
        UPDATE dbo.subscriptions
        SET messages_used_this_period = messages_used_this_period + @quantity
        WHERE id = @subscription_id;
    END

    SELECT 'Recorded' AS status, @subscription_id AS subscription_id;
END;
GO

-- Procedure: Get billing summary for a site
CREATE OR ALTER PROCEDURE dbo.sp_GetBillingSummary
    @site_id NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- Current subscription
    SELECT
        sub.id AS subscription_id,
        sub.status,
        sub.billing_cycle,
        sub.current_period_start,
        sub.current_period_end,
        sub.is_trial,
        sub.trial_end,
        sub.cancel_at_period_end,
        sub.price_at_subscription AS current_price,
        sub.currency,
        p.id AS plan_id,
        p.name AS plan_name,
        p.code AS plan_code,
        p.max_agents,
        p.max_conversations_per_month,
        p.max_file_storage_mb,
        sub.conversations_used_this_period,
        sub.messages_used_this_period,
        sub.storage_used_mb
    FROM dbo.subscriptions sub
    INNER JOIN dbo.subscription_plans p ON sub.plan_id = p.id
    WHERE sub.site_id = @site_id
    AND sub.status IN ('active', 'trialing', 'past_due');

    -- Payment methods
    SELECT
        id,
        type,
        card_brand,
        card_last_four,
        card_exp_month,
        card_exp_year,
        is_default,
        created_at
    FROM dbo.payment_methods
    WHERE site_id = @site_id
    AND is_active = 1
    ORDER BY is_default DESC, created_at DESC;

    -- Recent invoices
    SELECT TOP 10
        id,
        invoice_number,
        status,
        total,
        amount_due,
        currency,
        invoice_date,
        due_date,
        paid_at
    FROM dbo.invoices
    WHERE site_id = @site_id
    ORDER BY invoice_date DESC;

    -- Recent payments
    SELECT TOP 10
        id,
        amount,
        currency,
        status,
        card_brand,
        card_last_four,
        created_at,
        succeeded_at
    FROM dbo.payments
    WHERE site_id = @site_id
    ORDER BY created_at DESC;
END;
GO

-- Procedure: Generate next invoice number
CREATE OR ALTER PROCEDURE dbo.sp_GenerateInvoiceNumber
    @invoice_number NVARCHAR(50) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @year NVARCHAR(4) = CAST(YEAR(GETDATE()) AS NVARCHAR(4));
    DECLARE @sequence INT;

    -- Get the last invoice number for this year
    SELECT @sequence = ISNULL(MAX(
        CAST(SUBSTRING(invoice_number, 10, 5) AS INT)
    ), 0) + 1
    FROM dbo.invoices
    WHERE invoice_number LIKE 'INV-' + @year + '-%';

    SET @invoice_number = 'INV-' + @year + '-' + RIGHT('00000' + CAST(@sequence AS NVARCHAR(5)), 5);
END;
GO

-- Procedure: Get usage report for billing period
CREATE OR ALTER PROCEDURE dbo.sp_GetUsageReport
    @site_id NVARCHAR(50),
    @start_date DATETIME2 = NULL,
    @end_date DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Default to current subscription period
    IF @start_date IS NULL OR @end_date IS NULL
    BEGIN
        SELECT
            @start_date = current_period_start,
            @end_date = current_period_end
        FROM dbo.subscriptions
        WHERE site_id = @site_id
        AND status IN ('active', 'trialing');
    END

    -- Summary by usage type
    SELECT
        usage_type,
        SUM(quantity) AS total_quantity,
        COUNT(*) AS record_count,
        MIN(recorded_at) AS first_usage,
        MAX(recorded_at) AS last_usage
    FROM dbo.usage_records
    WHERE site_id = @site_id
    AND recorded_at BETWEEN @start_date AND @end_date
    GROUP BY usage_type;

    -- Daily breakdown
    SELECT
        CAST(recorded_at AS DATE) AS date,
        usage_type,
        SUM(quantity) AS quantity
    FROM dbo.usage_records
    WHERE site_id = @site_id
    AND recorded_at BETWEEN @start_date AND @end_date
    GROUP BY CAST(recorded_at AS DATE), usage_type
    ORDER BY date, usage_type;
END;
GO

-- Procedure: Cancel subscription
CREATE OR ALTER PROCEDURE dbo.sp_CancelSubscription
    @subscription_id NVARCHAR(50),
    @cancel_immediately BIT = 0,
    @reason NVARCHAR(500) = NULL,
    @performed_by_type NVARCHAR(20) = 'user',
    @performed_by_id NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @site_id NVARCHAR(50);
    DECLARE @current_status NVARCHAR(20);
    DECLARE @plan_id NVARCHAR(50);

    SELECT
        @site_id = site_id,
        @current_status = status,
        @plan_id = plan_id
    FROM dbo.subscriptions
    WHERE id = @subscription_id;

    IF @current_status IS NULL
    BEGIN
        RAISERROR('Subscription not found', 16, 1);
        RETURN;
    END

    IF @current_status IN ('canceled', 'incomplete_expired')
    BEGIN
        RAISERROR('Subscription is already canceled', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;

    IF @cancel_immediately = 1
    BEGIN
        UPDATE dbo.subscriptions
        SET
            status = 'canceled',
            canceled_at = SYSDATETIME(),
            cancellation_reason = @reason,
            updated_at = SYSDATETIME()
        WHERE id = @subscription_id;
    END
    ELSE
    BEGIN
        UPDATE dbo.subscriptions
        SET
            cancel_at_period_end = 1,
            cancellation_reason = @reason,
            updated_at = SYSDATETIME()
        WHERE id = @subscription_id;
    END

    -- Record in history
    INSERT INTO dbo.subscription_history (
        subscription_id, site_id, action,
        previous_status, new_status, previous_plan_id,
        details, performed_by_type, performed_by_id
    )
    VALUES (
        @subscription_id, @site_id, 'canceled',
        @current_status,
        CASE WHEN @cancel_immediately = 1 THEN 'canceled' ELSE @current_status END,
        @plan_id,
        N'{"reason": "' + ISNULL(@reason, '') + '", "immediate": ' + CASE WHEN @cancel_immediately = 1 THEN 'true' ELSE 'false' END + '}',
        @performed_by_type, @performed_by_id
    );

    COMMIT TRANSACTION;

    SELECT 'Subscription canceled successfully' AS message;
END;
GO

-- ============================================
-- INITIAL DATA / SEEDS
-- ============================================

-- Insert a default site for testing
INSERT INTO dbo.sites (id, name, domain)
VALUES ('site_local_test', 'Local Test Site', 'http://localhost:8000');

-- Insert default admin user (password: admin123)
-- Note: In production, use proper password hashing (bcrypt, etc.)
INSERT INTO dbo.users (id, username, email, password_hash, role, first_name, last_name)
VALUES (
    'user_admin',
    'admin',
    'admin@example.com',
    '$2b$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/X4.VTtYyJC0x/HWXe', -- admin123 (bcrypt)
    'admin',
    'System',
    'Admin'
);

-- Insert platform super admin (password: superadmin123)
-- This user has access to view all sites and users across the platform
INSERT INTO dbo.users (id, username, email, password_hash, role, first_name, last_name)
VALUES (
    'user_superadmin',
    'superadmin',
    'superadmin@chatapp.com',
    '$2b$12$tvHl6VMRbn4Nx4ZC8yEKqOkyR5IYq.Zr3MEzO4zlJJZPDl2jj/6uO', -- superadmin123 (bcrypt)
    'super_admin',
    'Platform',
    'Owner'
);

-- Insert sample support agents (password: password)
INSERT INTO dbo.users (id, username, email, password_hash, role, first_name, last_name)
VALUES
    ('user_alice', 'alice', 'alice@example.com', '$2b$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'support_agent', 'Alice', 'Smith'),
    ('user_bob', 'bob', 'bob@example.com', '$2b$12$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'support_agent', 'Bob', 'Johnson');

-- Assign users to the test site
INSERT INTO dbo.user_sites (user_id, site_id, can_manage_settings)
VALUES
    ('user_admin', 'site_local_test', 1),
    ('user_alice', 'site_local_test', 0),
    ('user_bob', 'site_local_test', 0);

-- Insert some canned responses
INSERT INTO dbo.canned_responses (site_id, title, content, shortcut, category)
VALUES
    ('site_local_test', 'Greeting', 'Hello! Thank you for reaching out. How can I help you today?', '/hello', 'Greetings'),
    ('site_local_test', 'Closing', 'Is there anything else I can help you with?', '/closing', 'Closings'),
    ('site_local_test', 'Thank You', 'Thank you for your patience. Let me look into this for you.', '/thanks', 'General'),
    ('site_local_test', 'Escalation', 'I understand this is important. Let me escalate this to our specialist team for you.', '/escalate', 'Support');
GO

-- ============================================
-- SUBSCRIPTION PLANS SEED DATA
-- ============================================

-- Insert subscription plans
INSERT INTO dbo.subscription_plans (id, name, code, description, price_monthly, price_yearly, max_agents, max_conversations_per_month, max_messages_per_month, max_sites, max_file_storage_mb, max_file_size_mb, message_history_days, trial_days, display_order, is_popular, features)
VALUES
    ('plan_free', 'Free', 'free', 'Perfect for getting started', 0, 0, 1, 50, 500, 1, 50, 2, 7, 0, 1, 0, N'{
        "ai_analysis": false,
        "custom_branding": false,
        "remove_powered_by": false,
        "priority_support": false,
        "api_access": false,
        "webhooks": false,
        "export_data": false,
        "advanced_analytics": false,
        "custom_domain": false,
        "canned_responses": 5,
        "email_notifications": true
    }'),
    ('plan_starter', 'Starter', 'starter', 'Great for small teams', 29, 290, 3, 500, 5000, 2, 500, 5, 30, 14, 2, 0, N'{
        "ai_analysis": true,
        "custom_branding": true,
        "remove_powered_by": false,
        "priority_support": false,
        "api_access": false,
        "webhooks": false,
        "export_data": true,
        "advanced_analytics": false,
        "custom_domain": false,
        "canned_responses": 25,
        "email_notifications": true
    }'),
    ('plan_pro', 'Professional', 'pro', 'Best for growing businesses', 79, 790, 10, 2000, NULL, 5, 2000, 10, 90, 14, 3, 1, N'{
        "ai_analysis": true,
        "custom_branding": true,
        "remove_powered_by": true,
        "priority_support": true,
        "api_access": true,
        "webhooks": true,
        "export_data": true,
        "advanced_analytics": true,
        "custom_domain": false,
        "canned_responses": 100,
        "email_notifications": true,
        "sms_notifications": true
    }'),
    ('plan_enterprise', 'Enterprise', 'enterprise', 'For large organizations with custom needs', 199, 1990, 50, NULL, NULL, 20, 10000, 25, 365, 14, 4, 0, N'{
        "ai_analysis": true,
        "custom_branding": true,
        "remove_powered_by": true,
        "priority_support": true,
        "api_access": true,
        "webhooks": true,
        "export_data": true,
        "advanced_analytics": true,
        "custom_domain": true,
        "canned_responses": -1,
        "email_notifications": true,
        "sms_notifications": true,
        "dedicated_support": true,
        "sla_guarantee": true,
        "custom_integrations": true
    }');
GO

-- Insert features
INSERT INTO dbo.features (id, code, name, description, category, is_metered, unit_name, display_order)
VALUES
    ('feat_ai', 'ai_analysis', 'AI Analysis', 'AI-powered conversation analysis and suggested replies', 'analytics', 0, NULL, 1),
    ('feat_branding', 'custom_branding', 'Custom Branding', 'Customize widget colors and appearance', 'customization', 0, NULL, 2),
    ('feat_nologo', 'remove_powered_by', 'Remove Branding', 'Remove "Powered by" attribution', 'customization', 0, NULL, 3),
    ('feat_priority', 'priority_support', 'Priority Support', '24/7 priority customer support', 'support', 0, NULL, 4),
    ('feat_api', 'api_access', 'API Access', 'Full REST API access for custom integrations', 'integrations', 0, NULL, 5),
    ('feat_webhooks', 'webhooks', 'Webhooks', 'Real-time event notifications via webhooks', 'integrations', 0, NULL, 6),
    ('feat_export', 'export_data', 'Data Export', 'Export conversations and analytics data', 'core', 0, NULL, 7),
    ('feat_analytics', 'advanced_analytics', 'Advanced Analytics', 'Detailed reports and insights', 'analytics', 0, NULL, 8),
    ('feat_domain', 'custom_domain', 'Custom Domain', 'Host widget on your own domain', 'customization', 0, NULL, 9),
    ('feat_conversations', 'conversations', 'Monthly Conversations', 'Number of conversations per month', 'core', 1, 'conversations', 10),
    ('feat_messages', 'messages', 'Monthly Messages', 'Number of messages per month', 'core', 1, 'messages', 11),
    ('feat_agents', 'agents', 'Support Agents', 'Number of support agent seats', 'core', 1, 'agents', 12),
    ('feat_storage', 'file_storage', 'File Storage', 'Storage space for uploaded files', 'core', 1, 'MB', 13);
GO

-- Link features to plans
INSERT INTO dbo.plan_features (plan_id, feature_id, is_enabled, limit_value)
VALUES
    -- Free plan
    ('plan_free', 'feat_conversations', 1, 50),
    ('plan_free', 'feat_messages', 1, 500),
    ('plan_free', 'feat_agents', 1, 1),
    ('plan_free', 'feat_storage', 1, 50),

    -- Starter plan
    ('plan_starter', 'feat_ai', 1, NULL),
    ('plan_starter', 'feat_branding', 1, NULL),
    ('plan_starter', 'feat_export', 1, NULL),
    ('plan_starter', 'feat_conversations', 1, 500),
    ('plan_starter', 'feat_messages', 1, 5000),
    ('plan_starter', 'feat_agents', 1, 3),
    ('plan_starter', 'feat_storage', 1, 500),

    -- Pro plan
    ('plan_pro', 'feat_ai', 1, NULL),
    ('plan_pro', 'feat_branding', 1, NULL),
    ('plan_pro', 'feat_nologo', 1, NULL),
    ('plan_pro', 'feat_priority', 1, NULL),
    ('plan_pro', 'feat_api', 1, NULL),
    ('plan_pro', 'feat_webhooks', 1, NULL),
    ('plan_pro', 'feat_export', 1, NULL),
    ('plan_pro', 'feat_analytics', 1, NULL),
    ('plan_pro', 'feat_conversations', 1, 2000),
    ('plan_pro', 'feat_messages', 1, NULL), -- Unlimited
    ('plan_pro', 'feat_agents', 1, 10),
    ('plan_pro', 'feat_storage', 1, 2000),

    -- Enterprise plan
    ('plan_enterprise', 'feat_ai', 1, NULL),
    ('plan_enterprise', 'feat_branding', 1, NULL),
    ('plan_enterprise', 'feat_nologo', 1, NULL),
    ('plan_enterprise', 'feat_priority', 1, NULL),
    ('plan_enterprise', 'feat_api', 1, NULL),
    ('plan_enterprise', 'feat_webhooks', 1, NULL),
    ('plan_enterprise', 'feat_export', 1, NULL),
    ('plan_enterprise', 'feat_analytics', 1, NULL),
    ('plan_enterprise', 'feat_domain', 1, NULL),
    ('plan_enterprise', 'feat_conversations', 1, NULL), -- Unlimited
    ('plan_enterprise', 'feat_messages', 1, NULL), -- Unlimited
    ('plan_enterprise', 'feat_agents', 1, 50),
    ('plan_enterprise', 'feat_storage', 1, 10000);
GO

-- Insert sample coupons
INSERT INTO dbo.coupons (id, code, name, description, discount_type, discount_value, duration, duration_months, max_redemptions, valid_until)
VALUES
    ('coupon_welcome', 'WELCOME20', 'Welcome Discount', '20% off your first 3 months', 'percentage', 20, 'repeating', 3, 1000, DATEADD(YEAR, 1, GETDATE())),
    ('coupon_annual', 'ANNUAL30', 'Annual Discount', '30% off annual plans', 'percentage', 30, 'once', NULL, NULL, NULL),
    ('coupon_startup', 'STARTUP50', 'Startup Special', '50% off for 6 months', 'percentage', 50, 'repeating', 6, 100, DATEADD(MONTH, 6, GETDATE()));
GO

-- Create sample subscription for test site (on Free plan)
INSERT INTO dbo.subscriptions (
    id, site_id, plan_id, status, billing_cycle,
    current_period_start, current_period_end,
    price_at_subscription, currency
)
VALUES (
    'sub_test_local', 'site_local_test', 'plan_free', 'active', 'monthly',
    SYSDATETIME(), DATEADD(MONTH, 1, SYSDATETIME()),
    0, 'USD'
);
GO

-- ============================================
-- HELPFUL QUERIES
-- ============================================

/*
-- ==================== CHAT QUERIES ====================

-- Get all active conversations for a site with unread counts
SELECT
    c.id,
    v.name AS visitor_name,
    c.status,
    c.message_count,
    c.last_message_at,
    ISNULL(uc.unread_count, 0) AS unread_count
FROM dbo.conversations c
INNER JOIN dbo.visitors v ON c.visitor_id = v.id
LEFT JOIN dbo.v_unread_counts uc ON c.id = uc.conversation_id
WHERE c.site_id = 'site_local_test'
AND c.status = 'active'
ORDER BY c.last_message_at DESC;

-- Get online agents for a site
SELECT * FROM dbo.v_online_agents WHERE site_id = 'site_local_test';

-- Get conversation history
EXEC dbo.sp_GetConversationWithMessages @conversation_id = 'conv_xxx', @messages_limit = 50;

-- Get dashboard stats for today
EXEC dbo.sp_GetDashboardStats
    @site_id = 'site_local_test',
    @start_date = '2024-01-01',
    @end_date = '2024-12-31';


-- ==================== SUBSCRIPTION & BILLING QUERIES ====================

-- Get all subscription plans with pricing
SELECT
    id, name, code, description,
    price_monthly, price_yearly,
    max_agents, max_conversations_per_month, max_sites,
    trial_days, is_popular, is_active
FROM dbo.subscription_plans
WHERE is_active = 1
ORDER BY display_order;

-- Get site subscription with plan details
SELECT * FROM dbo.v_site_subscriptions WHERE site_id = 'site_local_test';

-- Get monthly recurring revenue (MRR) by plan
SELECT * FROM dbo.v_monthly_revenue;

-- Get total MRR across all plans
SELECT SUM(total_mrr) AS total_mrr FROM dbo.v_monthly_revenue;

-- Get billing summary for a site
EXEC dbo.sp_GetBillingSummary @site_id = 'site_local_test';

-- Check if site can create more conversations
EXEC dbo.sp_CheckSubscriptionLimit
    @site_id = 'site_local_test',
    @limit_type = 'conversations',
    @quantity = 1;

-- Record conversation usage
EXEC dbo.sp_RecordUsage
    @site_id = 'site_local_test',
    @usage_type = 'conversations',
    @quantity = 1,
    @resource_type = 'conversation',
    @resource_id = 'conv_xxx';

-- Get usage report for current billing period
EXEC dbo.sp_GetUsageReport @site_id = 'site_local_test';

-- Get all invoices for a site
SELECT * FROM dbo.v_invoice_summary WHERE site_id = 'site_local_test' ORDER BY invoice_date DESC;

-- Get unpaid invoices
SELECT * FROM dbo.invoices WHERE status IN ('open', 'past_due') ORDER BY due_date;

-- Get subscriptions expiring soon (next 7 days)
SELECT
    s.id AS site_id,
    s.name AS site_name,
    sub.id AS subscription_id,
    sub.current_period_end,
    sub.cancel_at_period_end,
    p.name AS plan_name
FROM dbo.subscriptions sub
INNER JOIN dbo.sites s ON sub.site_id = s.id
INNER JOIN dbo.subscription_plans p ON sub.plan_id = p.id
WHERE sub.status = 'active'
AND sub.current_period_end BETWEEN GETDATE() AND DATEADD(DAY, 7, GETDATE());

-- Get trials ending soon
SELECT
    s.id AS site_id,
    s.name AS site_name,
    sub.trial_end,
    DATEDIFF(DAY, GETDATE(), sub.trial_end) AS days_remaining,
    p.name AS plan_name
FROM dbo.subscriptions sub
INNER JOIN dbo.sites s ON sub.site_id = s.id
INNER JOIN dbo.subscription_plans p ON sub.plan_id = p.id
WHERE sub.status = 'trialing'
AND sub.trial_end BETWEEN GETDATE() AND DATEADD(DAY, 7, GETDATE());

-- Get coupon usage statistics
SELECT
    c.code,
    c.name,
    c.discount_type,
    c.discount_value,
    c.current_redemptions,
    c.max_redemptions,
    SUM(cr.discount_amount) AS total_discount_given
FROM dbo.coupons c
LEFT JOIN dbo.coupon_redemptions cr ON c.id = cr.coupon_id
GROUP BY c.id, c.code, c.name, c.discount_type, c.discount_value, c.current_redemptions, c.max_redemptions;

-- Get failed payments in last 30 days
SELECT
    p.id,
    s.name AS site_name,
    p.amount,
    p.failure_code,
    p.failure_message,
    p.created_at
FROM dbo.payments p
INNER JOIN dbo.sites s ON p.site_id = s.id
WHERE p.status = 'failed'
AND p.created_at >= DATEADD(DAY, -30, GETDATE())
ORDER BY p.created_at DESC;

-- Revenue report for a date range
SELECT
    CAST(p.succeeded_at AS DATE) AS date,
    COUNT(*) AS transaction_count,
    SUM(p.amount) AS gross_revenue,
    SUM(p.stripe_fee) AS total_fees,
    SUM(p.net_amount) AS net_revenue
FROM dbo.payments p
WHERE p.status = 'succeeded'
AND p.succeeded_at BETWEEN '2024-01-01' AND '2024-12-31'
GROUP BY CAST(p.succeeded_at AS DATE)
ORDER BY date;

-- Subscription churn analysis
SELECT
    DATEPART(YEAR, canceled_at) AS year,
    DATEPART(MONTH, canceled_at) AS month,
    COUNT(*) AS cancellations,
    COUNT(CASE WHEN cancellation_reason LIKE '%price%' THEN 1 END) AS price_related,
    COUNT(CASE WHEN cancellation_reason LIKE '%feature%' THEN 1 END) AS feature_related
FROM dbo.subscriptions
WHERE canceled_at IS NOT NULL
GROUP BY DATEPART(YEAR, canceled_at), DATEPART(MONTH, canceled_at)
ORDER BY year DESC, month DESC;

*/

-- ============================================
-- SITE SETTINGS TABLE (Platform-wide settings)
-- ============================================
-- Stores platform-wide branding and SEO settings
-- This is a single-row table for global configuration

IF OBJECT_ID('dbo.site_settings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.site_settings (
        id NVARCHAR(50) NOT NULL PRIMARY KEY DEFAULT ('settings_' + LOWER(CONVERT(NVARCHAR(36), NEWID()))),

        -- Branding
        site_name NVARCHAR(255) NOT NULL DEFAULT 'ChatApp',
        site_logo NVARCHAR(500) NULL,
        favicon NVARCHAR(500) NULL,
        copyright_text NVARCHAR(255) NULL,

        -- SEO Settings
        seo_title NVARCHAR(255) NULL,
        seo_description NVARCHAR(500) NULL,
        seo_keywords NVARCHAR(500) NULL,
        og_image NVARCHAR(500) NULL,

        -- Social Media Links
        facebook_url NVARCHAR(500) NULL,
        twitter_url NVARCHAR(500) NULL,
        linked_in_url NVARCHAR(500) NULL,
        instagram_url NVARCHAR(500) NULL,

        -- Contact Info
        support_email NVARCHAR(255) NULL,
        support_phone NVARCHAR(50) NULL,
        support_address NVARCHAR(255) NULL,

        -- Feature Flags
        feature_supervisor_mode BIT NOT NULL DEFAULT 1,
        feature_ai_analysis BIT NOT NULL DEFAULT 1,
        feature_ai_auto_reply BIT NOT NULL DEFAULT 1,
        feature_file_sharing BIT NOT NULL DEFAULT 1,
        feature_csat_ratings BIT NOT NULL DEFAULT 1,
        feature_visitor_info BIT NOT NULL DEFAULT 1,
        feature_canned_responses BIT NOT NULL DEFAULT 1,
        feature_conversation_transfer BIT NOT NULL DEFAULT 1,
        feature_team_chat BIT NOT NULL DEFAULT 1,
        feature_typing_indicators BIT NOT NULL DEFAULT 1,
        feature_read_receipts BIT NOT NULL DEFAULT 1,
        feature_internal_notes BIT NOT NULL DEFAULT 1,
        feature_emoji_picker BIT NOT NULL DEFAULT 1,
        feature_email_sending BIT NOT NULL DEFAULT 1,
        feature_conversation_search BIT NOT NULL DEFAULT 1,
        feature_message_search BIT NOT NULL DEFAULT 1,
        feature_bulk_actions BIT NOT NULL DEFAULT 1,
        feature_themes BIT NOT NULL DEFAULT 1,
        feature_agent_status BIT NOT NULL DEFAULT 1,
        feature_notifications BIT NOT NULL DEFAULT 1,

        -- Additional Settings (JSON for extensibility)
        additional_settings NVARCHAR(MAX) NULL,

        -- Timestamps
        created_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        updated_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );

    PRINT 'Created site_settings table';
END
GO

-- ============================================
-- DEMO REQUESTS TABLE
-- ============================================
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
GO

PRINT 'Database schema created successfully!';
GO
