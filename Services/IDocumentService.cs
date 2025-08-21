namespace RaiToolbox.Services;

public interface IDocumentService
{
    Task<DocumentProcessingResult> ProcessDocumentAsync(IFormFile file, string userId);
    Task<string> ExtractTextFromDocumentAsync(string filePath);
    Task<List<DocumentInfo>> GetUserDocumentsAsync(string userId);
    Task<bool> DeleteDocumentAsync(string documentId, string userId);
}

public class DocumentProcessingResult
{
    public bool Success { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public int TextLength { get; set; }
    public string? Error { get; set; }
}

public class DocumentInfo
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public int TextLength { get; set; }
    public string UserId { get; set; } = string.Empty;
}