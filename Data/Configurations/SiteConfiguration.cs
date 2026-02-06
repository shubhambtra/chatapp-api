using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasMaxLength(36);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Domain).IsRequired().HasMaxLength(255);
        builder.Property(s => s.ApiKey).IsRequired().HasMaxLength(100);

        builder.Property(s => s.OwnerUserId).HasMaxLength(36);
        builder.Property(s => s.CompanyName).HasMaxLength(200);
        builder.Property(s => s.CompanyWebsite).HasMaxLength(255);
        builder.Property(s => s.CompanySize).HasMaxLength(50);
        builder.Property(s => s.Industry).HasMaxLength(100);

        // Billing properties
        builder.Property(s => s.BillingEmail).HasMaxLength(255);
        builder.Property(s => s.BillingName).HasMaxLength(200);
        builder.Property(s => s.BillingPhone).HasMaxLength(50);
        builder.Property(s => s.BillingAddressLine1).HasMaxLength(255);
        builder.Property(s => s.BillingAddressLine2).HasMaxLength(255);
        builder.Property(s => s.BillingCity).HasMaxLength(100);
        builder.Property(s => s.BillingState).HasMaxLength(100);
        builder.Property(s => s.BillingPostalCode).HasMaxLength(20);
        builder.Property(s => s.BillingCountry).HasMaxLength(100);
        builder.Property(s => s.TaxId).HasMaxLength(50);

        builder.Property(s => s.StripeCustomerId).HasMaxLength(100);
        builder.Property(s => s.WidgetConfig).HasColumnType("nvarchar(max)");
        builder.Property(s => s.OnboardingState).HasColumnType("nvarchar(max)");
        builder.Property(s => s.Status).IsRequired().HasMaxLength(20).HasDefaultValue("active");
        builder.Property(s => s.Timezone).IsRequired().HasMaxLength(50).HasDefaultValue("UTC");
        builder.Property(s => s.BusinessHours).HasColumnType("nvarchar(max)");
        builder.Property(s => s.AiModel).HasMaxLength(50).HasDefaultValue("gpt-4o-mini");
        builder.Property(s => s.AllowedFileTypes).HasMaxLength(500);

        builder.HasOne(s => s.OwnerUser)
            .WithMany()
            .HasForeignKey(s => s.OwnerUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => s.Domain);
        builder.HasIndex(s => s.ApiKey).IsUnique();
    }
}
