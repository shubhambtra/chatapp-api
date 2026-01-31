using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasMaxLength(36);

        builder.Property(m => m.ConversationId).IsRequired().HasMaxLength(36);
        builder.Property(m => m.SenderType).IsRequired().HasMaxLength(20);
        builder.Property(m => m.SenderId).HasMaxLength(36);
        builder.Property(m => m.Content).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(m => m.MessageType).IsRequired().HasMaxLength(20).HasDefaultValue("text");
        builder.Property(m => m.FileId).HasMaxLength(36);
        builder.Property(m => m.Metadata).HasColumnType("nvarchar(max)");

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.File)
            .WithMany(f => f.Messages)
            .HasForeignKey(m => m.FileId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(m => m.ConversationId);
        builder.HasIndex(m => m.CreatedAt);
    }
}

public class MessageReadConfiguration : IEntityTypeConfiguration<MessageRead>
{
    public void Configure(EntityTypeBuilder<MessageRead> builder)
    {
        builder.ToTable("message_reads");

        builder.HasKey(mr => mr.Id);

        builder.Property(mr => mr.MessageId).IsRequired().HasMaxLength(36);
        builder.Property(mr => mr.ReaderType).IsRequired().HasMaxLength(20);
        builder.Property(mr => mr.ReaderId).IsRequired().HasMaxLength(36);

        builder.HasOne(mr => mr.Message)
            .WithMany(m => m.ReadReceipts)
            .HasForeignKey(mr => mr.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(mr => new { mr.MessageId, mr.ReaderId }).IsUnique();
    }
}

public class ChatFileConfiguration : IEntityTypeConfiguration<ChatFile>
{
    public void Configure(EntityTypeBuilder<ChatFile> builder)
    {
        builder.ToTable("files");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasMaxLength(36);

        builder.Property(f => f.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(f => f.UploaderType).IsRequired().HasMaxLength(20);
        builder.Property(f => f.UploaderId).IsRequired().HasMaxLength(36);
        builder.Property(f => f.OriginalName).IsRequired().HasMaxLength(255);
        builder.Property(f => f.StoredName).IsRequired().HasMaxLength(255);
        builder.Property(f => f.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(f => f.FilePath).IsRequired().HasMaxLength(500);
        builder.Property(f => f.ThumbnailPath).HasMaxLength(500);

        builder.HasOne(f => f.Site)
            .WithMany()
            .HasForeignKey(f => f.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.SiteId);
    }
}
