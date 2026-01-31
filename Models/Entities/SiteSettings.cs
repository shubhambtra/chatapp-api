using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.API.Models.Entities;

/// <summary>
/// Platform-wide site settings (single row table)
/// </summary>
public class SiteSettings : BaseEntity
{
    // Branding
    [Column("site_name")]
    public string SiteName { get; set; } = "Assistica AI";

    [Column("site_logo")]
    public string? SiteLogo { get; set; }

    [Column("favicon")]
    public string? Favicon { get; set; }

    [Column("copyright_text")]
    public string? CopyrightText { get; set; }

    // SEO Settings
    [Column("seo_title")]
    public string? SeoTitle { get; set; }

    [Column("seo_description")]
    public string? SeoDescription { get; set; }

    [Column("seo_keywords")]
    public string? SeoKeywords { get; set; }

    [Column("og_image")]
    public string? OgImage { get; set; }

    // Social Media Links
    [Column("facebook_url")]
    public string? FacebookUrl { get; set; }

    [Column("twitter_url")]
    public string? TwitterUrl { get; set; }

    [Column("linked_in_url")]
    public string? LinkedInUrl { get; set; }

    [Column("instagram_url")]
    public string? InstagramUrl { get; set; }

    // Contact Info
    [Column("support_email")]
    public string? SupportEmail { get; set; }

    [Column("support_phone")]
    public string? SupportPhone { get; set; }

    [Column("support_address")]
    public string? SupportAddress { get; set; }

    // Feature Flags
    [Column("feature_supervisor_mode")]
    public bool FeatureSupervisorMode { get; set; } = true;

    [Column("feature_ai_analysis")]
    public bool FeatureAiAnalysis { get; set; } = true;

    [Column("feature_ai_auto_reply")]
    public bool FeatureAiAutoReply { get; set; } = true;

    [Column("feature_file_sharing")]
    public bool FeatureFileSharing { get; set; } = true;

    [Column("feature_csat_ratings")]
    public bool FeatureCsatRatings { get; set; } = true;

    [Column("feature_visitor_info")]
    public bool FeatureVisitorInfo { get; set; } = true;

    [Column("feature_canned_responses")]
    public bool FeatureCannedResponses { get; set; } = true;

    [Column("feature_conversation_transfer")]
    public bool FeatureConversationTransfer { get; set; } = true;

    [Column("feature_team_chat")]
    public bool FeatureTeamChat { get; set; } = true;

    [Column("feature_typing_indicators")]
    public bool FeatureTypingIndicators { get; set; } = true;

    [Column("feature_read_receipts")]
    public bool FeatureReadReceipts { get; set; } = true;

    [Column("feature_internal_notes")]
    public bool FeatureInternalNotes { get; set; } = true;

    [Column("feature_emoji_picker")]
    public bool FeatureEmojiPicker { get; set; } = true;

    [Column("feature_email_sending")]
    public bool FeatureEmailSending { get; set; } = true;

    [Column("feature_conversation_search")]
    public bool FeatureConversationSearch { get; set; } = true;

    [Column("feature_message_search")]
    public bool FeatureMessageSearch { get; set; } = true;

    [Column("feature_bulk_actions")]
    public bool FeatureBulkActions { get; set; } = true;

    [Column("feature_themes")]
    public bool FeatureThemes { get; set; } = true;

    [Column("feature_agent_status")]
    public bool FeatureAgentStatus { get; set; } = true;

    [Column("feature_notifications")]
    public bool FeatureNotifications { get; set; } = true;

    // Additional Settings (JSON for extensibility)
    [Column("additional_settings")]
    public string? AdditionalSettings { get; set; }
}
