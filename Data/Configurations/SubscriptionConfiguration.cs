using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");

        builder.HasKey(sp => sp.Id);
        builder.Property(sp => sp.Id).HasMaxLength(36);

        builder.Property(sp => sp.Name).IsRequired().HasMaxLength(100);
        builder.Property(sp => sp.Description).HasMaxLength(500);
        builder.Property(sp => sp.MonthlyPrice).HasColumnType("decimal(10,2)");
        builder.Property(sp => sp.AnnualPrice).HasColumnType("decimal(10,2)");
        builder.Property(sp => sp.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("USD");
        builder.Property(sp => sp.MonthlyPriceInr).HasColumnType("decimal(18,2)");
        builder.Property(sp => sp.AnnualPriceInr).HasColumnType("decimal(18,2)");
        builder.Property(sp => sp.InrEnabled).HasDefaultValue(false);
        builder.Property(sp => sp.StripeMonthlyPriceId).HasMaxLength(100);
        builder.Property(sp => sp.StripeAnnualPriceId).HasMaxLength(100);

        builder.HasIndex(sp => sp.Name).IsUnique();
    }
}

public class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasMaxLength(36);

        builder.Property(f => f.Name).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Code).IsRequired().HasMaxLength(50);
        builder.Property(f => f.Description).HasMaxLength(500);
        builder.Property(f => f.Category).IsRequired().HasMaxLength(50).HasDefaultValue("general");

        builder.HasIndex(f => f.Code).IsUnique();
    }
}

public class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.ToTable("plan_features");

        builder.HasKey(pf => pf.Id);

        builder.Property(pf => pf.PlanId).IsRequired().HasMaxLength(36);
        builder.Property(pf => pf.FeatureId).IsRequired().HasMaxLength(36);
        builder.Property(pf => pf.LimitValue).HasMaxLength(50);

        builder.HasOne(pf => pf.Plan)
            .WithMany(p => p.PlanFeatures)
            .HasForeignKey(pf => pf.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pf => pf.Feature)
            .WithMany(f => f.PlanFeatures)
            .HasForeignKey(pf => pf.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pf => new { pf.PlanId, pf.FeatureId }).IsUnique();
    }
}

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasMaxLength(36);

        builder.Property(s => s.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(s => s.PlanId).IsRequired().HasMaxLength(36);
        builder.Property(s => s.Status).IsRequired().HasMaxLength(20).HasDefaultValue("active");
        builder.Property(s => s.BillingCycle).IsRequired().HasMaxLength(20).HasDefaultValue("monthly");
        builder.Property(s => s.StripeSubscriptionId).HasMaxLength(100);

        builder.HasOne(s => s.Site)
            .WithMany(site => site.Subscriptions)
            .HasForeignKey(s => s.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Plan)
            .WithMany(p => p.Subscriptions)
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(s => s.SiteId);
        builder.HasIndex(s => s.StripeSubscriptionId);
    }
}

public class SubscriptionHistoryConfiguration : IEntityTypeConfiguration<SubscriptionHistory>
{
    public void Configure(EntityTypeBuilder<SubscriptionHistory> builder)
    {
        builder.ToTable("subscription_history");

        builder.HasKey(sh => sh.Id);

        builder.Property(sh => sh.SubscriptionId).IsRequired().HasMaxLength(36);
        builder.Property(sh => sh.Action).IsRequired().HasMaxLength(50);
        builder.Property(sh => sh.FromPlanId).HasMaxLength(36);
        builder.Property(sh => sh.ToPlanId).HasMaxLength(36);
        builder.Property(sh => sh.Reason).HasMaxLength(500);
        builder.Property(sh => sh.Metadata).HasColumnType("nvarchar(max)");

        builder.HasOne(sh => sh.Subscription)
            .WithMany(s => s.History)
            .HasForeignKey(sh => sh.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.ToTable("usage_records");

        builder.HasKey(ur => ur.Id);

        builder.Property(ur => ur.SubscriptionId).IsRequired().HasMaxLength(36);
        builder.Property(ur => ur.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(ur => ur.MetricName).IsRequired().HasMaxLength(50);

        builder.HasOne(ur => ur.Subscription)
            .WithMany(s => s.UsageRecords)
            .HasForeignKey(ur => ur.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Site)
            .WithMany()
            .HasForeignKey(ur => ur.SiteId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(ur => new { ur.SiteId, ur.MetricName, ur.PeriodStart });
    }
}
