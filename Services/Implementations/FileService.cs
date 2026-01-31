using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class FileService : IFileService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ISubscriptionService _subscriptionService;

    public FileService(ApplicationDbContext context, IConfiguration configuration, IWebHostEnvironment environment, ISubscriptionService subscriptionService)
    {
        _context = context;
        _configuration = configuration;
        _environment = environment;
        _subscriptionService = subscriptionService;
    }

    public async Task<UploadFileResponse> UploadFileAsync(string siteId, string uploaderType, string uploaderId, IFormFile file)
    {
        if (!await ValidateFileAsync(siteId, file))
        {
            throw new InvalidOperationException("File validation failed");
        }

        var uploadPath = _configuration["FileUpload:UploadPath"] ?? "uploads";
        var fullPath = Path.Combine(_environment.ContentRootPath, uploadPath, siteId);

        Directory.CreateDirectory(fullPath);

        var storedName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(fullPath, storedName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var chatFile = new ChatFile
        {
            SiteId = siteId,
            UploaderType = uploaderType,
            UploaderId = uploaderId,
            OriginalName = file.FileName,
            StoredName = storedName,
            MimeType = file.ContentType,
            FileSize = file.Length,
            FilePath = filePath
        };

        // Generate thumbnail for images
        if (file.ContentType.StartsWith("image/"))
        {
            // In production, use ImageSharp or similar for thumbnail generation
            // For now, we'll skip thumbnail generation
        }

        _context.Files.Add(chatFile);
        await _context.SaveChangesAsync();

        return new UploadFileResponse(
            chatFile.Id,
            chatFile.OriginalName,
            chatFile.MimeType,
            chatFile.FileSize,
            $"/api/files/{chatFile.Id}",
            chatFile.ThumbnailPath != null ? $"/api/files/{chatFile.Id}/thumbnail" : null
        );
    }

    public async Task<FileDto?> GetFileAsync(string fileId)
    {
        var file = await _context.Files.FindAsync(fileId);
        if (file == null || file.IsDeleted) return null;

        return new FileDto(
            file.Id,
            file.OriginalName,
            file.MimeType,
            file.FileSize,
            $"/api/files/{file.Id}",
            file.ThumbnailPath != null ? $"/api/files/{file.Id}/thumbnail" : null,
            file.Width,
            file.Height,
            file.CreatedAt
        );
    }

    public async Task<Stream?> GetFileStreamAsync(string fileId)
    {
        var file = await _context.Files.FindAsync(fileId);
        if (file == null || file.IsDeleted) return null;

        if (!File.Exists(file.FilePath)) return null;

        return new FileStream(file.FilePath, FileMode.Open, FileAccess.Read);
    }

    public async Task<Stream?> GetThumbnailStreamAsync(string fileId)
    {
        var file = await _context.Files.FindAsync(fileId);
        if (file == null || file.IsDeleted || string.IsNullOrEmpty(file.ThumbnailPath)) return null;

        if (!File.Exists(file.ThumbnailPath)) return null;

        return new FileStream(file.ThumbnailPath, FileMode.Open, FileAccess.Read);
    }

    public async Task DeleteFileAsync(string fileId)
    {
        var file = await _context.Files.FindAsync(fileId);
        if (file == null) throw new KeyNotFoundException("File not found");

        file.IsDeleted = true;
        file.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Optionally delete physical file
        // if (File.Exists(file.FilePath)) File.Delete(file.FilePath);
    }

    public async Task<bool> ValidateFileAsync(string siteId, IFormFile file)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) return false;

        // Check file size from subscription plan
        var plan = await _subscriptionService.GetSitePlanAsync(siteId);
        var maxSizeMb = plan?.MaxFileSizeMb ?? site.MaxFileSizeMb; // Fall back to site setting if no plan
        if (file.Length > maxSizeMb * 1024 * 1024)
        {
            return false;
        }

        // Check file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = site.AllowedFileTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLowerInvariant())
            .ToList();

        if (!allowedExtensions.Contains(extension))
        {
            return false;
        }

        return true;
    }
}
