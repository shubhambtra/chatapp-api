using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("payment_methods");

        builder.HasKey(pm => pm.Id);
        builder.Property(pm => pm.Id).HasMaxLength(36);

        builder.Property(pm => pm.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(pm => pm.Type).IsRequired().HasMaxLength(20).HasDefaultValue("card");
        builder.Property(pm => pm.Last4).HasMaxLength(4);
        builder.Property(pm => pm.Brand).HasMaxLength(50);
        builder.Property(pm => pm.BankName).HasMaxLength(100);
        builder.Property(pm => pm.StripePaymentMethodId).HasMaxLength(100);

        builder.HasOne(pm => pm.Site)
            .WithMany(s => s.PaymentMethods)
            .HasForeignKey(pm => pm.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pm => pm.SiteId);
    }
}

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasMaxLength(36);

        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.DiscountType).IsRequired().HasMaxLength(20).HasDefaultValue("percentage");
        builder.Property(c => c.DiscountValue).HasColumnType("decimal(10,2)");
        builder.Property(c => c.Currency).HasMaxLength(3);
        builder.Property(c => c.StripeCouponId).HasMaxLength(100);

        builder.HasIndex(c => c.Code).IsUnique();
    }
}

public class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("coupon_redemptions");

        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.CouponId).IsRequired().HasMaxLength(36);
        builder.Property(cr => cr.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(cr => cr.SubscriptionId).HasMaxLength(36);
        builder.Property(cr => cr.DiscountAmount).HasColumnType("decimal(10,2)");

        builder.HasOne(cr => cr.Coupon)
            .WithMany(c => c.Redemptions)
            .HasForeignKey(cr => cr.CouponId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cr => cr.Site)
            .WithMany()
            .HasForeignKey(cr => cr.SiteId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(cr => cr.Subscription)
            .WithMany()
            .HasForeignKey(cr => cr.SubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasMaxLength(36);

        builder.Property(i => i.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(i => i.SubscriptionId).HasMaxLength(36);
        builder.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Status).IsRequired().HasMaxLength(20).HasDefaultValue("draft");
        builder.Property(i => i.Subtotal).HasColumnType("decimal(10,2)");
        builder.Property(i => i.Tax).HasColumnType("decimal(10,2)");
        builder.Property(i => i.Discount).HasColumnType("decimal(10,2)");
        builder.Property(i => i.Total).HasColumnType("decimal(10,2)");
        builder.Property(i => i.AmountPaid).HasColumnType("decimal(10,2)");
        builder.Property(i => i.AmountDue).HasColumnType("decimal(10,2)");
        builder.Property(i => i.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.Property(i => i.StripeInvoiceId).HasMaxLength(100);
        builder.Property(i => i.StripeInvoiceUrl).HasMaxLength(500);
        builder.Property(i => i.StripeInvoicePdf).HasMaxLength(500);

        builder.HasOne(i => i.Site)
            .WithMany(s => s.Invoices)
            .HasForeignKey(i => i.SiteId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(i => i.Subscription)
            .WithMany()
            .HasForeignKey(i => i.SubscriptionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasIndex(i => i.SiteId);
    }
}

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("invoice_items");

        builder.HasKey(ii => ii.Id);

        builder.Property(ii => ii.InvoiceId).IsRequired().HasMaxLength(36);
        builder.Property(ii => ii.Description).IsRequired().HasMaxLength(500);
        builder.Property(ii => ii.UnitPrice).HasColumnType("decimal(10,2)");
        builder.Property(ii => ii.Amount).HasColumnType("decimal(10,2)");

        builder.HasOne(ii => ii.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasMaxLength(36);

        builder.Property(p => p.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(p => p.InvoiceId).HasMaxLength(36);
        builder.Property(p => p.PaymentMethodId).HasMaxLength(36);
        builder.Property(p => p.Amount).HasColumnType("decimal(10,2)");
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(p => p.Status).IsRequired().HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.StripePaymentIntentId).HasMaxLength(100);
        builder.Property(p => p.StripeChargeId).HasMaxLength(100);

        builder.HasOne(p => p.Site)
            .WithMany()
            .HasForeignKey(p => p.SiteId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(p => p.PaymentMethod)
            .WithMany()
            .HasForeignKey(p => p.PaymentMethodId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(p => p.SiteId);
        builder.HasIndex(p => p.StripePaymentIntentId);
    }
}

public class PaymentRefundConfiguration : IEntityTypeConfiguration<PaymentRefund>
{
    public void Configure(EntityTypeBuilder<PaymentRefund> builder)
    {
        builder.ToTable("payment_refunds");

        builder.HasKey(pr => pr.Id);

        builder.Property(pr => pr.PaymentId).IsRequired().HasMaxLength(36);
        builder.Property(pr => pr.Amount).HasColumnType("decimal(10,2)");
        builder.Property(pr => pr.Reason).HasMaxLength(500);
        builder.Property(pr => pr.Status).IsRequired().HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(pr => pr.StripeRefundId).HasMaxLength(100);

        builder.HasOne(pr => pr.Payment)
            .WithMany(p => p.Refunds)
            .HasForeignKey(pr => pr.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
