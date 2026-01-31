using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasMaxLength(36);

        builder.Property(c => c.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(c => c.VisitorId).IsRequired().HasMaxLength(36);
        builder.Property(c => c.AssignedUserId).HasMaxLength(36);

        builder.Property(c => c.Status).IsRequired().HasMaxLength(20).HasDefaultValue("active");
        builder.Property(c => c.Priority).IsRequired().HasMaxLength(20).HasDefaultValue("normal");
        builder.Property(c => c.Channel).IsRequired().HasMaxLength(20).HasDefaultValue("widget");
        builder.Property(c => c.Subject).HasMaxLength(255);
        builder.Property(c => c.Tags).HasColumnType("nvarchar(max)");
        builder.Property(c => c.CustomData).HasColumnType("nvarchar(max)");
        builder.Property(c => c.Feedback).HasMaxLength(1000);

        builder.HasOne(c => c.Site)
            .WithMany(s => s.Conversations)
            .HasForeignKey(c => c.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Visitor)
            .WithMany(v => v.Conversations)
            .HasForeignKey(c => c.VisitorId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(c => c.AssignedUser)
            .WithMany(u => u.AssignedConversations)
            .HasForeignKey(c => c.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => new { c.SiteId, c.Status });
        builder.HasIndex(c => new { c.SiteId, c.AssignedUserId });
        builder.HasIndex(c => c.VisitorId);
    }
}

public class ConversationAnalysisConfiguration : IEntityTypeConfiguration<ConversationAnalysis>
{
    public void Configure(EntityTypeBuilder<ConversationAnalysis> builder)
    {
        builder.ToTable("conversation_analyses");

        builder.HasKey(ca => ca.Id);

        builder.Property(ca => ca.ConversationId).IsRequired().HasMaxLength(36);
        builder.Property(ca => ca.Summary).HasColumnType("nvarchar(max)");
        builder.Property(ca => ca.Sentiment).HasMaxLength(50);
        builder.Property(ca => ca.Topics).HasColumnType("nvarchar(max)");
        builder.Property(ca => ca.Intent).HasMaxLength(100);
        builder.Property(ca => ca.Language).HasMaxLength(10);
        builder.Property(ca => ca.SuggestedResponses).HasColumnType("nvarchar(max)");
        builder.Property(ca => ca.KeyPhrases).HasColumnType("nvarchar(max)");

        builder.HasOne(ca => ca.Conversation)
            .WithOne(c => c.Analysis)
            .HasForeignKey<ConversationAnalysis>(ca => ca.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
