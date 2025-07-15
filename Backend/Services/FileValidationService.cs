using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Backend.Services
{
    /// <summary>
    /// Service for validating uploaded files
    /// </summary>
    public interface IFileValidationService
    {
        /// <summary>
        /// Validates if a file meets the required criteria for processing
        /// </summary>
        /// <param name="file">The file to validate</param>
        /// <param name="strictValidation">If true, applies stricter validation rules</param>
        /// <returns>Tuple indicating validity and error message if any</returns>
        Task<(bool IsValid, string ErrorMessage)> ValidateFileAsync(IFormFile file, bool strictValidation = true);

        /// <summary>
        /// Checks if the file extension is supported
        /// </summary>
        /// <param name="fileName">Name of the file to check</param>
        /// <returns>True if supported, false otherwise</returns>
        bool IsValidFileExtension(string fileName);
    }

    /// <summary>
    /// Implementation of file validation service
    /// </summary>
    public class FileValidationService : IFileValidationService
    {
        private readonly ILogger<FileValidationService> _logger;
        private readonly string[] _allowedExtensions = new[] { ".pdf", ".docx", ".xlsx" };
        
        public FileValidationService(ILogger<FileValidationService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<(bool IsValid, string ErrorMessage)> ValidateFileAsync(IFormFile file, bool strictValidation = true)
        {
            // Check if file is null
            if (file == null)
            {
                _logger.LogError("File validation failed: File is null");
                return (false, "File is required but not provided");
            }

            // Check if file is empty
            if (file.Length == 0)
            {
                _logger.LogError("File validation failed: File is empty");
                return (false, "File is empty");
            }

            // Check file extension
            if (strictValidation && !IsValidFileExtension(file.FileName))
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                _logger.LogError("File validation failed: Unsupported extension {Extension}", extension);
                return (false, $"Unsupported file format: {extension}. Please upload a PDF, Word, or Excel file.");
            }

            // Verify the file can be read
            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var buffer = new byte[Math.Min(file.Length, 1024)]; // Read first 1KB to verify
                    // Using Memory<byte> with modern ReadAsync to avoid CA2022 warning
                    var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                    if (bytesRead == 0)
                    {
                        _logger.LogWarning("Could not read any bytes from file {FileName}", file.FileName);
                        return (false, "File appears to be empty or cannot be read");
                    }
                }
                _logger.LogInformation("File validation succeeded for {FileName}", file.FileName);
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File validation failed: Cannot read file {FileName}", file.FileName);
                return (false, $"File cannot be read: {ex.Message}");
            }
        }

        /// <inheritdoc />
        public bool IsValidFileExtension(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
                
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return _allowedExtensions.Contains(extension);
        }
    }
}
