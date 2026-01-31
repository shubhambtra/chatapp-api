namespace ChatApp.API.Models.Entities;

public class WelcomeMessage : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string Message { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public int DelayMs { get; set; } = 0; // Delay before sending (for future use)
}
