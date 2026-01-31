using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class VisitorConfiguration : IEntityTypeConfiguration<Visitor>
{
    public void Configure(EntityTypeBuilder<Visitor> builder)
    {
        builder.ToTable("visitors");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasMaxLength(36);

        builder.Property(v => v.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(v => v.ExternalId).HasMaxLength(100);
        builder.Property(v => v.Email).HasMaxLength(255);
        builder.Property(v => v.Name).HasMaxLength(200);
        builder.Property(v => v.Phone).HasMaxLength(50);
        builder.Property(v => v.AvatarUrl).HasMaxLength(500);

        builder.Property(v => v.UserAgent).HasMaxLength(500);
        builder.Property(v => v.Browser).HasMaxLength(50);
        builder.Property(v => v.BrowserVersion).HasMaxLength(20);
        builder.Property(v => v.Os).HasMaxLength(50);
        builder.Property(v => v.OsVersion).HasMaxLength(20);
        builder.Property(v => v.DeviceType).HasMaxLength(20);

        builder.Property(v => v.IpAddress).HasMaxLength(45);
        builder.Property(v => v.Country).HasMaxLength(100);
        builder.Property(v => v.Region).HasMaxLength(100);
        builder.Property(v => v.City).HasMaxLength(100);
        builder.Property(v => v.Timezone).HasMaxLength(50);

        builder.Property(v => v.ReferrerUrl).HasMaxLength(500);
        builder.Property(v => v.LandingPage).HasMaxLength(500);
        builder.Property(v => v.CurrentPage).HasMaxLength(500);

        builder.Property(v => v.Tags).HasColumnType("nvarchar(max)");
        builder.Property(v => v.CustomData).HasColumnType("nvarchar(max)");

        builder.HasOne(v => v.Site)
            .WithMany(s => s.Visitors)
            .HasForeignKey(v => v.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.SiteId, v.Email });
        builder.HasIndex(v => new { v.SiteId, v.ExternalId });
    }
}

public class VisitorSessionConfiguration : IEntityTypeConfiguration<VisitorSession>
{
    public void Configure(EntityTypeBuilder<VisitorSession> builder)
    {
        builder.ToTable("visitor_sessions");

        builder.HasKey(vs => vs.Id);
        builder.Property(vs => vs.Id).HasMaxLength(36);

        builder.Property(vs => vs.VisitorId).IsRequired().HasMaxLength(36);
        builder.Property(vs => vs.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(vs => vs.WebsocketId).HasMaxLength(100);
        builder.Property(vs => vs.IpAddress).HasMaxLength(45);
        builder.Property(vs => vs.UserAgent).HasMaxLength(500);
        builder.Property(vs => vs.CurrentPage).HasMaxLength(500);
        builder.Property(vs => vs.ReferrerUrl).HasMaxLength(500);

        builder.HasOne(vs => vs.Visitor)
            .WithMany(v => v.Sessions)
            .HasForeignKey(vs => vs.VisitorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(vs => vs.Site)
            .WithMany()
            .HasForeignKey(vs => vs.SiteId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
