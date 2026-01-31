using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasMaxLength(36);

        builder.Property(u => u.Username).IsRequired().HasMaxLength(50);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(255);
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.Role).IsRequired().HasMaxLength(50).HasDefaultValue("support_agent");
        builder.Property(u => u.Status).IsRequired().HasMaxLength(20).HasDefaultValue("offline");
        builder.Property(u => u.NotificationPreferences).HasColumnType("nvarchar(max)");

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Username).IsUnique();

        builder.Ignore(u => u.FullName);
        builder.Ignore(u => u.Initials);
    }
}

public class UserSiteConfiguration : IEntityTypeConfiguration<UserSite>
{
    public void Configure(EntityTypeBuilder<UserSite> builder)
    {
        builder.ToTable("user_sites");

        builder.HasKey(us => us.Id);

        builder.Property(us => us.UserId).IsRequired().HasMaxLength(36);
        builder.Property(us => us.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(us => us.AssignedBy).HasMaxLength(36);

        builder.HasOne(us => us.User)
            .WithMany(u => u.UserSites)
            .HasForeignKey(us => us.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(us => us.Site)
            .WithMany(s => s.UserSites)
            .HasForeignKey(us => us.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(us => new { us.UserId, us.SiteId }).IsUnique();
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.UserId).IsRequired().HasMaxLength(36);
        builder.Property(rt => rt.Token).IsRequired().HasMaxLength(500);
        builder.Property(rt => rt.DeviceInfo).HasMaxLength(500);
        builder.Property(rt => rt.IpAddress).HasMaxLength(45);

        builder.HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rt => rt.Token);
    }
}

public class AgentSessionConfiguration : IEntityTypeConfiguration<AgentSession>
{
    public void Configure(EntityTypeBuilder<AgentSession> builder)
    {
        builder.ToTable("agent_sessions");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasMaxLength(36);

        builder.Property(a => a.UserId).IsRequired().HasMaxLength(36);
        builder.Property(a => a.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(a => a.Token).IsRequired().HasMaxLength(500);
        builder.Property(a => a.WebsocketId).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(500);

        builder.HasOne(a => a.User)
            .WithMany(u => u.AgentSessions)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Site)
            .WithMany()
            .HasForeignKey(a => a.SiteId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
