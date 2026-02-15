-- Add payment gateway enable/disable flags to app_settings table
ALTER TABLE app_settings ADD razorpay_enabled BIT NOT NULL DEFAULT 0;
ALTER TABLE app_settings ADD paypal_enabled BIT NOT NULL DEFAULT 0;
