using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data.Configurations;

public class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("knowledge_documents");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasMaxLength(36);

        builder.Property(d => d.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(d => d.Title).IsRequired().HasMaxLength(500);
        builder.Property(d => d.Description).HasMaxLength(2000);
        builder.Property(d => d.DocumentType).IsRequired().HasMaxLength(20).HasDefaultValue("text");

        builder.Property(d => d.OriginalFileName).HasMaxLength(500);
        builder.Property(d => d.StoredFileName).HasMaxLength(500);
        builder.Property(d => d.FilePath).HasMaxLength(1000);
        builder.Property(d => d.MimeType).HasMaxLength(100);

        builder.Property(d => d.RawContent).HasColumnType("nvarchar(max)");
        builder.Property(d => d.ExtractedText).HasColumnType("nvarchar(max)");

        builder.Property(d => d.Status).IsRequired().HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(d => d.ProcessingError).HasMaxLength(2000);

        builder.HasOne(d => d.Site)
            .WithMany()
            .HasForeignKey(d => d.SiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.SiteId);
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => new { d.SiteId, d.IsDeleted });
    }
}

public class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("knowledge_chunks");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasMaxLength(36);

        builder.Property(c => c.DocumentId).IsRequired().HasMaxLength(36);
        builder.Property(c => c.SiteId).IsRequired().HasMaxLength(36);
        builder.Property(c => c.Content).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(c => c.EmbeddingJson).HasColumnType("nvarchar(max)");

        builder.HasOne(c => c.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Site)
            .WithMany()
            .HasForeignKey(c => c.SiteId)
            .OnDelete(DeleteBehavior.NoAction); // Prevent multiple cascade paths

        builder.HasIndex(c => c.DocumentId);
        builder.HasIndex(c => c.SiteId);
        builder.HasIndex(c => new { c.SiteId, c.DocumentId });
    }
}
