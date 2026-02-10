using Microsoft.EntityFrameworkCore;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Core entities
    public DbSet<User> Users => Set<User>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<UserSite> UserSites => Set<UserSite>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AgentSession> AgentSessions => Set<AgentSession>();

    // Visitor entities
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<VisitorSession> VisitorSessions => Set<VisitorSession>();

    // Conversation entities
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationAnalysis> ConversationAnalyses => Set<ConversationAnalysis>();
    public DbSet<ConversationComment> ConversationComments => Set<ConversationComment>();

    // Message entities
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageRead> MessageReads => Set<MessageRead>();
    public DbSet<ChatFile> Files => Set<ChatFile>();

    // Notification entities
    public DbSet<Notification> Notifications => Set<Notification>();

    // Welcome Message entities
    public DbSet<WelcomeMessage> WelcomeMessages => Set<WelcomeMessage>();

    // Subscription entities
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionHistory> SubscriptionHistories => Set<SubscriptionHistory>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();

    // Payment entities
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentRefund> PaymentRefunds => Set<PaymentRefund>();
    public DbSet<PaymentLog> PaymentLogs => Set<PaymentLog>();

    // Knowledge Base entities
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    // Platform Settings
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();

    // Email Logs
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    // SMTP Settings
    public DbSet<SmtpSettings> SmtpSettings => Set<SmtpSettings>();

    // Contact Submissions
    public DbSet<ContactSubmission> ContactSubmissions => Set<ContactSubmission>();

    // Website Visits (Analytics)
    public DbSet<WebsiteVisit> WebsiteVisits => Set<WebsiteVisit>();

    // Tutorial Videos
    public DbSet<TutorialVideo> TutorialVideos => Set<TutorialVideo>();

    // Issue Reports
    public DbSet<IssueReport> IssueReports => Set<IssueReport>();
    public DbSet<IssueReportAttachment> IssueReportAttachments => Set<IssueReportAttachment>();

    // Demo Requests
    public DbSet<DemoRequest> DemoRequests => Set<DemoRequest>();

    // App Configuration
    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();

    // Workflows
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowExecution> WorkflowExecutions => Set<WorkflowExecution>();

    // Error Logs
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .ToTable("users", "dbo");

        modelBuilder.Entity<Site>()
            .ToTable("sites", "dbo");

        modelBuilder.Entity<UserSite>()
            .ToTable("user_sites", "dbo");

        modelBuilder.Entity<RefreshToken>()
            .ToTable("refresh_tokens", "dbo");

        modelBuilder.Entity<AgentSession>()
            .ToTable("agent_sessions", "dbo");

        modelBuilder.Entity<Visitor>()
            .ToTable("visitors", "dbo");

        modelBuilder.Entity<VisitorSession>()
            .ToTable("visitor_sessions", "dbo");

        modelBuilder.Entity<WelcomeMessage>()
            .ToTable("welcome_messages", "dbo");

        modelBuilder.Entity<SiteSettings>(entity =>
        {
            entity.ToTable("site_settings", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<PaymentLog>()
            .ToTable("payment_logs", "dbo");

        modelBuilder.Entity<SmtpSettings>(entity =>
        {
            entity.ToTable("smtp_settings", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ContactSubmission>(entity =>
        {
            entity.ToTable("contact_submissions", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<WebsiteVisit>(entity =>
        {
            entity.ToTable("website_visits", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.IpAddress);
            entity.HasIndex(e => e.VisitedAt);
        });

        modelBuilder.Entity<TutorialVideo>(entity =>
        {
            entity.ToTable("tutorial_videos", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.DisplayOrder);
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<ConversationComment>(entity =>
        {
            entity.ToTable("conversation_comments", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.AuthorName).HasColumnName("author_name");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Mentions).HasColumnName("mentions");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.AuthorId);
        });

        modelBuilder.Entity<IssueReport>(entity =>
        {
            entity.ToTable("issue_reports", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasMany(e => e.Attachments)
                .WithOne(a => a.IssueReport)
                .HasForeignKey(a => a.IssueReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IssueReportAttachment>(entity =>
        {
            entity.ToTable("issue_report_attachments", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<DemoRequest>(entity =>
        {
            entity.ToTable("demo_requests", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<AppConfiguration>(entity =>
        {
            entity.ToTable("app_settings", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                entity.Id = Guid.NewGuid().ToString();
                entity.CreatedAt = DateTime.UtcNow;
            }

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
