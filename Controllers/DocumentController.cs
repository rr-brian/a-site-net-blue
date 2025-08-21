using Microsoft.AspNetCore.Mvc;
using RaiToolbox.Services;

namespace RaiToolbox.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IDocumentService documentService,
        IAuthenticationService authService,
        ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file provided" });
            }

            var userId = _authService.GetUserId(HttpContext);
            var result = await _documentService.ProcessDocumentAsync(file, userId);

            if (result.Success)
            {
                return Ok(new
                {
                    documentId = result.DocumentId,
                    fileName = result.FileName,
                    textLength = result.TextLength,
                    message = "Document uploaded and processed successfully"
                });
            }

            return BadRequest(new { error = result.Error });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(500, new { error = "An error occurred while uploading the document" });
        }
    }

    [HttpPost("upload-multiple")]
    public async Task<IActionResult> UploadMultipleDocuments(List<IFormFile> files)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { error = "No files provided" });
            }

            var userId = _authService.GetUserId(HttpContext);
            var results = new List<object>();

            foreach (var file in files)
            {
                var result = await _documentService.ProcessDocumentAsync(file, userId);
                results.Add(new
                {
                    fileName = file.FileName,
                    success = result.Success,
                    documentId = result.DocumentId,
                    textLength = result.TextLength,
                    error = result.Error
                });
            }

            return Ok(new { results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading multiple documents");
            return StatusCode(500, new { error = "An error occurred while uploading documents" });
        }
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetUserDocuments()
    {
        try
        {
            var userId = _authService.GetUserId(HttpContext);
            var documents = await _documentService.GetUserDocumentsAsync(userId);

            return Ok(documents.Select(d => new
            {
                id = d.Id,
                fileName = d.FileName,
                fileType = d.FileType,
                fileSizeBytes = d.FileSizeBytes,
                uploadedAt = d.UploadedAt,
                textLength = d.TextLength
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user documents");
            return StatusCode(500, new { error = "An error occurred while retrieving documents" });
        }
    }

    [HttpDelete("{documentId}")]
    public async Task<IActionResult> DeleteDocument(string documentId)
    {
        try
        {
            var userId = _authService.GetUserId(HttpContext);
            var success = await _documentService.DeleteDocumentAsync(documentId, userId);

            if (success)
            {
                return Ok(new { message = "Document deleted successfully" });
            }

            return NotFound(new { error = "Document not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting document {documentId}");
            return StatusCode(500, new { error = "An error occurred while deleting the document" });
        }
    }
}