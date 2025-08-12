using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Backend.Services.Interfaces;

namespace Backend.Services
{

    /// <summary>
    /// Implementation of the request diagnostics service
    /// </summary>
    public class RequestDiagnosticsService : Interfaces.IRequestDiagnosticsService
    {
        private readonly ILogger<RequestDiagnosticsService> _logger;
        
        public RequestDiagnosticsService(ILogger<RequestDiagnosticsService> logger)
        {
            _logger = logger;
        }
        
        /// <inheritdoc />
        public void LogRequestDetails(HttpContext context)
        {
            try
            {
                // Log basic request info
                _logger.LogInformation("Request: {Method} {Path}{QueryString}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString);
                
                // Log important headers
                _logger.LogInformation("Content-Type: {ContentType}, Content-Length: {ContentLength}",
                    context.Request.ContentType,
                    context.Request.ContentLength);
                
                // Log user agent
                _logger.LogInformation("User-Agent: {UserAgent}",
                    context.Request.Headers.ContainsKey("User-Agent") ?
                        context.Request.Headers["User-Agent"].ToString() : "<not provided>");
                
                // Log connection info
                _logger.LogInformation("Connection details - IsHttps: {IsHttps}, RemoteIP: {RemoteIP}",
                    context.Request.IsHttps,
                    context.Connection.RemoteIpAddress);
                
                // Log session/auth info if available
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    _logger.LogInformation("User: {User}, Auth Type: {AuthType}",
                        context.User.Identity.Name,
                        context.User.Identity.AuthenticationType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging request details");
            }
        }
        
        /// <inheritdoc />
        public async Task<Dictionary<string, string>> ExtractFormDataAsync(HttpContext context)
        {
            var result = new Dictionary<string, string>();
            
            try
            {
                // Only process if it's a form
                if (context.Request.HasFormContentType)
                {
                    var form = await context.Request.ReadFormAsync();
                    
                    // Log keys available
                    _logger.LogInformation("Form data keys: {Keys}", string.Join(", ", form.Keys));
                    
                    // Extract non-file values
                    foreach (var key in form.Keys.Where(k => 
                        !k.Equals("file", StringComparison.OrdinalIgnoreCase)))
                    {
                        var value = form[key].ToString();
                        result[key] = value;
                        
                        // Log but truncate for privacy/size
                        var logValue = value.Length > 50 ? value.Substring(0, 47) + "..." : value;
                        _logger.LogInformation("Form key '{Key}' has value: {Value}", key, logValue);
                    }
                    
                    // Log file count
                    _logger.LogInformation("Form contains {Count} files", form.Files.Count);
                }
                else
                {
                    _logger.LogInformation("Request does not contain form data");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting form data");
            }
            
            return result;
        }
        
        /// <inheritdoc />
        public void LogFileDetails(IFormFile file)
        {
            if (file == null)
            {
                _logger.LogWarning("Attempted to log details of null file");
                return;
            }
            
            try
            {
                _logger.LogInformation("File details - Name: {Name}, FileName: {FileName}, ContentType: {ContentType}, Length: {Length} bytes",
                    file.Name,
                    file.FileName,
                    file.ContentType,
                    file.Length);
                
                // Check file extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                _logger.LogInformation("File extension: {Extension}", extension);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging file details");
            }
        }
    }
}
