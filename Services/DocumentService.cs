using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace RaiToolbox.Services;

public class DocumentService : IDocumentService
{
    private readonly ILogger<DocumentService> _logger;
    private readonly string _uploadPath;
    private readonly Dictionary<string, List<DocumentInfo>> _userDocuments;

    public DocumentService(ILogger<DocumentService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _userDocuments = new Dictionary<string, List<DocumentInfo>>();
        
        // Ensure upload directory exists
        Directory.CreateDirectory(_uploadPath);
    }

    public async Task<DocumentProcessingResult> ProcessDocumentAsync(IFormFile file, string userId)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return new DocumentProcessingResult
                {
                    Success = false,
                    Error = "No file provided"
                };
            }

            var allowedExtensions = new[] { ".pdf", ".txt", ".doc", ".docx" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                return new DocumentProcessingResult
                {
                    Success = false,
                    Error = "Unsupported file type"
                };
            }

            var documentId = Guid.NewGuid().ToString();
            var fileName = $"{documentId}_{file.FileName}";
            var filePath = Path.Combine(_uploadPath, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Extract text
            var extractedText = await ExtractTextFromDocumentAsync(filePath);

            // Store document info
            var documentInfo = new DocumentInfo
            {
                Id = documentId,
                FileName = file.FileName,
                FileType = fileExtension,
                FileSizeBytes = file.Length,
                UploadedAt = DateTime.UtcNow,
                TextLength = extractedText.Length,
                UserId = userId
            };

            if (!_userDocuments.ContainsKey(userId))
            {
                _userDocuments[userId] = new List<DocumentInfo>();
            }
            _userDocuments[userId].Add(documentInfo);

            _logger.LogInformation($"Document processed successfully: {file.FileName} for user {userId}");

            return new DocumentProcessingResult
            {
                Success = true,
                DocumentId = documentId,
                FileName = file.FileName,
                ExtractedText = extractedText,
                TextLength = extractedText.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing document: {file?.FileName}");
            return new DocumentProcessingResult
            {
                Success = false,
                Error = $"Processing error: {ex.Message}"
            };
        }
    }

    public async Task<string> ExtractTextFromDocumentAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        try
        {
            return extension switch
            {
                ".txt" => await File.ReadAllTextAsync(filePath),
                ".pdf" => await ExtractTextFromPdfAsync(filePath),
                ".doc" or ".docx" => await ExtractTextFromWordAsync(filePath),
                _ => throw new NotSupportedException($"File type {extension} not supported")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extracting text from {filePath}");
            return $"Error extracting text: {ex.Message}";
        }
    }

    private async Task<string> ExtractTextFromPdfAsync(string filePath)
    {
        try
        {
            _logger.LogInformation($"Extracting text from PDF: {filePath}");
            
            await Task.Run(() => {
                // This operation is CPU intensive, so run on thread pool
            });
            
            using var pdfReader = new PdfReader(filePath);
            using var pdfDocument = new PdfDocument(pdfReader);
            
            var text = new StringBuilder();
            int pageCount = pdfDocument.GetNumberOfPages();
            
            _logger.LogInformation($"PDF has {pageCount} pages");
            
            for (int i = 1; i <= pageCount; i++)
            {
                var page = pdfDocument.GetPage(i);
                var pageText = PdfTextExtractor.GetTextFromPage(page);
                text.AppendLine($"--- Page {i} ---");
                text.AppendLine(pageText);
                text.AppendLine();
            }
            
            var extractedText = text.ToString().Trim();
            _logger.LogInformation($"Successfully extracted {extractedText.Length} characters from PDF");
            
            return string.IsNullOrWhiteSpace(extractedText) 
                ? $"[No readable text found in {Path.GetFileName(filePath)}]"
                : extractedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extracting text from PDF: {filePath}");
            return $"[Error reading PDF {Path.GetFileName(filePath)}: {ex.Message}]";
        }
    }

    private async Task<string> ExtractTextFromWordAsync(string filePath)
    {
        // For now, return a placeholder - would need library like DocumentFormat.OpenXml
        _logger.LogWarning("Word document text extraction not implemented - returning placeholder");
        await Task.Delay(1);
        return $"[Word Document Content from {Path.GetFileName(filePath)} - Text extraction would require Word library]";
    }

    public async Task<List<DocumentInfo>> GetUserDocumentsAsync(string userId)
    {
        await Task.Delay(1);
        return _userDocuments.ContainsKey(userId) ? _userDocuments[userId] : new List<DocumentInfo>();
    }

    public async Task<bool> DeleteDocumentAsync(string documentId, string userId)
    {
        try
        {
            if (!_userDocuments.ContainsKey(userId))
                return false;

            var document = _userDocuments[userId].FirstOrDefault(d => d.Id == documentId);
            if (document == null)
                return false;

            // Remove from memory
            _userDocuments[userId].Remove(document);

            // Delete file
            var fileName = $"{documentId}_{document.FileName}";
            var filePath = Path.Combine(_uploadPath, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _logger.LogInformation($"Document deleted: {document.FileName} for user {userId}");
            await Task.Delay(1);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting document {documentId}");
            return false;
        }
    }
}