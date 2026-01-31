using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasMaxLength(36);

        builder.Property(n => n.UserId).IsRequired().HasMaxLength(36);
        builder.Property(n => n.SiteId).HasMaxLength(36);
        builder.Property(n => n.Type).IsRequired().HasMaxLength(50);
        builder.Property(n => n.Title).IsRequired().HasMaxLength(255);
        builder.Property(n => n.Message).HasMaxLength(500);
        builder.Property(n => n.ActionUrl).HasMaxLength(500);
        builder.Property(n => n.Data).HasColumnType("nvarchar(max)");
        builder.Property(n => n.ConversationId).HasMaxLength(36);

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Site)
            .WithMany()
            .HasForeignKey(n => n.SiteId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(n => n.Conversation)
            .WithMany()
            .HasForeignKey(n => n.ConversationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(n => new { n.UserId, n.IsRead });
        builder.HasIndex(n => n.CreatedAt);
    }
}
