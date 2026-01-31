using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost("upload")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UploadFileResponse>>> Upload(
        [FromQuery] string siteId,
        IFormFile file)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<UploadFileResponse>.Fail("User not found"));
        }

        try
        {
            var result = await _fileService.UploadFileAsync(siteId, "agent", userId, file);
            return Ok(ApiResponse<UploadFileResponse>.Ok(result, "File uploaded"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UploadFileResponse>.Fail(ex.Message));
        }
    }

    [HttpPost("upload/visitor")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<UploadFileResponse>>> UploadAsVisitor(
        [FromQuery] string siteId,
        [FromQuery] string visitorId,
        IFormFile file)
    {
        try
        {
            var result = await _fileService.UploadFileAsync(siteId, "visitor", visitorId, file);
            return Ok(ApiResponse<UploadFileResponse>.Ok(result, "File uploaded"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UploadFileResponse>.Fail(ex.Message));
        }
    }

    [HttpGet("{fileId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFile(string fileId)
    {
        var fileInfo = await _fileService.GetFileAsync(fileId);
        if (fileInfo == null)
        {
            return NotFound();
        }

        var stream = await _fileService.GetFileStreamAsync(fileId);
        if (stream == null)
        {
            return NotFound();
        }

        return File(stream, fileInfo.MimeType, fileInfo.OriginalName);
    }

    [HttpGet("{fileId}/info")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<FileDto>>> GetFileInfo(string fileId)
    {
        var fileInfo = await _fileService.GetFileAsync(fileId);
        if (fileInfo == null)
        {
            return NotFound(ApiResponse<FileDto>.Fail("File not found"));
        }

        return Ok(ApiResponse<FileDto>.Ok(fileInfo));
    }

    [HttpGet("{fileId}/thumbnail")]
    [AllowAnonymous]
    public async Task<IActionResult> GetThumbnail(string fileId)
    {
        var stream = await _fileService.GetThumbnailStreamAsync(fileId);
        if (stream == null)
        {
            return NotFound();
        }

        return File(stream, "image/jpeg");
    }

    [HttpDelete("{fileId}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> DeleteFile(string fileId)
    {
        try
        {
            await _fileService.DeleteFileAsync(fileId);
            return Ok(ApiResponse<object>.Ok(null, "File deleted"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }
}
