using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasMaxLength(36);
        builder.Property(e => e.FromEmail).HasMaxLength(255).IsRequired();
        builder.Property(e => e.FromName).HasMaxLength(255);
        builder.Property(e => e.ToEmail).HasMaxLength(255).IsRequired();
        builder.Property(e => e.ToName).HasMaxLength(255);
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("sent");
        builder.Property(e => e.EmailType).HasMaxLength(100);
        builder.Property(e => e.SiteId).HasMaxLength(36);
        builder.Property(e => e.UserId).HasMaxLength(36);

        builder.HasIndex(e => e.CreatedAt).IsDescending();
        builder.HasIndex(e => e.ToEmail);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.EmailType);
        builder.HasIndex(e => e.SiteId);

        builder.HasOne(e => e.Site)
            .WithMany()
            .HasForeignKey(e => e.SiteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
