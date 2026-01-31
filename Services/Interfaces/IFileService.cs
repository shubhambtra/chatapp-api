using ChatApp.API.Models.DTOs;
using Microsoft.AspNetCore.Http;

namespace ChatApp.API.Services.Interfaces;

public interface IFileService
{
    Task<UploadFileResponse> UploadFileAsync(string siteId, string uploaderType, string uploaderId, IFormFile file);
    Task<FileDto?> GetFileAsync(string fileId);
    Task<Stream?> GetFileStreamAsync(string fileId);
    Task<Stream?> GetThumbnailStreamAsync(string fileId);
    Task DeleteFileAsync(string fileId);
    Task<bool> ValidateFileAsync(string siteId, IFormFile file);
}
