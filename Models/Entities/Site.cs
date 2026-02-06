namespace ChatApp.API.Models.Entities;

public class Site : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    // Owner
    public string? OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    // Company details
    public string? CompanyName { get; set; }
    public string? CompanyWebsite { get; set; }
    public string? CompanySize { get; set; }
    public string? Industry { get; set; }

    // Billing contact
    public string? BillingEmail { get; set; }
    public string? BillingName { get; set; }
    public string? BillingPhone { get; set; }
    public string? BillingAddressLine1 { get; set; }
    public string? BillingAddressLine2 { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingPostalCode { get; set; }
    public string? BillingCountry { get; set; }

    // Tax
    public string? TaxId { get; set; }
    public bool TaxExempt { get; set; }

    // Stripe
    public string? StripeCustomerId { get; set; }

    // Registration payment reference (links registration payment to site)
    public string? PaymentReference { get; set; }

    // Widget config
    public string WidgetConfig { get; set; } = "{}";

    // Onboarding state
    public string? OnboardingState { get; set; }

    // Settings
    public string Status { get; set; } = "active";
    public string Timezone { get; set; } = "UTC";
    public string? BusinessHours { get; set; }

    // AI settings
    public bool AiEnabled { get; set; } = true;
    public string AiModel { get; set; } = "gpt-4o-mini";

    // AI toggle states (per-site operational settings)
    public bool AutoReplyEnabled { get; set; } = false;
    public bool AnalysisEnabled { get; set; } = false;

    // File settings
    public int MaxFileSizeMb { get; set; } = 10;
    public string AllowedFileTypes { get; set; } = ".jpg,.jpeg,.png,.gif,.webp,.pdf,.doc,.docx,.txt,.zip";

    // Navigation properties
    public ICollection<UserSite> UserSites { get; set; } = new List<UserSite>();
    public ICollection<Visitor> Visitors { get; set; } = new List<Visitor>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<PaymentMethod> PaymentMethods { get; set; } = new List<PaymentMethod>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
