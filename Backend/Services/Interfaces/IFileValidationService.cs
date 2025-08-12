using Microsoft.AspNetCore.Http;

namespace Backend.Services.Interfaces
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
}
