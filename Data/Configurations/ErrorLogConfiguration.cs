using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class ErrorLogConfiguration : IEntityTypeConfiguration<ErrorLog>
{
    public void Configure(EntityTypeBuilder<ErrorLog> builder)
    {
        builder.ToTable("error_logs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.ErrorMessage).IsRequired();
        builder.Property(e => e.StackTrace);
        builder.Property(e => e.Source).HasMaxLength(500);
        builder.Property(e => e.ErrorCode).HasMaxLength(50);
        builder.Property(e => e.RequestPath).HasMaxLength(500);
        builder.Property(e => e.RequestMethod).HasMaxLength(10);
        builder.Property(e => e.RequestBody);
        builder.Property(e => e.QueryString).HasMaxLength(2000);
        builder.Property(e => e.UserId).HasMaxLength(36);
        builder.Property(e => e.IpAddress).HasMaxLength(50);
        builder.Property(e => e.UserAgent).HasMaxLength(500);
        builder.Property(e => e.ExceptionType).HasMaxLength(255);
        builder.Property(e => e.InnerException);
        builder.Property(e => e.Severity).HasMaxLength(20).HasDefaultValue("Error").IsRequired();

        builder.HasIndex(e => e.CreatedAt).IsDescending();
        builder.HasIndex(e => e.RequestPath);
        builder.HasIndex(e => e.UserId);
    }
}
