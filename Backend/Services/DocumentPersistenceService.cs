using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Models;
using Backend.Services.Interfaces;

namespace Backend.Services
{
    /// <summary>
    /// Service for persisting document information between requests
    /// Uses a static in-memory store and an optional file-based backup
    /// </summary>
    public class DocumentPersistenceService : Interfaces.IDocumentPersistenceService
    {
        // Static store to ensure it persists across application restarts
        private static readonly ConcurrentDictionary<string, DocumentInfo> _documentStore = new();
        private readonly ILogger<DocumentPersistenceService> _logger;
        private readonly string _persistencePath;
        
        public DocumentPersistenceService(ILogger<DocumentPersistenceService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            // Create a directory for document persistence
            _persistencePath = Path.Combine(env.ContentRootPath, "App_Data", "Documents");
            
            // Ensure the directory exists
            if (!Directory.Exists(_persistencePath))
            {
                Directory.CreateDirectory(_persistencePath);
            }
        }
        
        /// <summary>
        /// Stores a document for a specific session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="document">Document to store</param>
        public void StoreDocument(string sessionId, DocumentInfo document)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    _logger.LogWarning("Cannot store document with null or empty session ID");
                    return;
                }
                
                if (document == null)
                {
                    _logger.LogWarning("Cannot store null document for session {SessionId}", sessionId);
                    return;
                }
                
                if (document.Chunks == null || document.Chunks.Count == 0)
                {
                    _logger.LogWarning("Cannot store document with no chunks for session {SessionId}", sessionId);
                    return;
                }
                
                // Create a more reliable deep copy using JSON serialization/deserialization to ensure complete isolation
                // This prevents any shared references between the original document and our stored copy
                DocumentInfo documentCopy;
                try {
                    var tempOptions = new JsonSerializerOptions {
                        WriteIndented = false,  // No need for pretty printing in memory
                        PropertyNamingPolicy = null, // Preserve property names
                        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    };
                    
                    // Serialize and deserialize to create a true deep copy with no shared references
                    var json = JsonSerializer.Serialize(document, tempOptions);
                    documentCopy = JsonSerializer.Deserialize<DocumentInfo>(json, tempOptions);
                    
                    if (documentCopy == null) {
                        _logger.LogError("Failed to create deep copy via JSON serialization for session {SessionId}", sessionId);
                        return; // Early exit if we can't make a copy
                    }
                    
                    // Sanity check the copy to ensure data integrity
                    if (documentCopy.Chunks == null || documentCopy.Chunks.Count == 0) {
                        _logger.LogError("Deep copy resulted in null or empty chunks for session {SessionId}", sessionId);
                        
                        // Fall back to manual deep copy to recover
                        documentCopy = new DocumentInfo {
                            FileName = document.FileName,
                            UploadTime = document.UploadTime,
                            Chunks = document.Chunks != null ? new List<string>(document.Chunks) : new List<string>(),
                            ChunkMetadata = document.ChunkMetadata != null 
                                ? new List<ChunkMetadata>(document.ChunkMetadata) 
                                : null,
                            EntityIndex = document.EntityIndex != null 
                                ? new Dictionary<string, List<int>>(document.EntityIndex.ToDictionary(
                                    kvp => kvp.Key, 
                                    kvp => new List<int>(kvp.Value)))
                                : null
                        };
                    }
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error creating deep copy for session {SessionId}: {Error}", sessionId, ex.Message);
                    
                    // Fall back to manual deep copy on exception
                    documentCopy = new DocumentInfo {
                        FileName = document.FileName,
                        UploadTime = document.UploadTime,
                        Chunks = document.Chunks != null ? new List<string>(document.Chunks) : new List<string>(),
                        ChunkMetadata = document.ChunkMetadata != null 
                            ? new List<ChunkMetadata>(document.ChunkMetadata) 
                            : null,
                        EntityIndex = document.EntityIndex != null 
                            ? new Dictionary<string, List<int>>(document.EntityIndex.ToDictionary(
                                kvp => kvp.Key, 
                                kvp => new List<int>(kvp.Value)))
                            : null
                    };
                }
                
                _logger.LogInformation("Storing document in memory for session ID: {SessionId} - Document: {FileName} with {ChunkCount} chunks", 
                    sessionId, documentCopy.FileName, documentCopy.Chunks.Count);
                    
                // Verify deep copy was successful
                if (documentCopy.Chunks.Count != document.Chunks.Count)
                {
                    _logger.LogWarning("Deep copy has different chunk count! Original: {OriginalCount}, Copy: {CopyCount}",
                        document.Chunks.Count, documentCopy.Chunks.Count);
                }
                
                // Store in memory
                _documentStore[sessionId] = documentCopy;
                
                // Also persist to disk for durability
                var filePath = GetDocumentFilePath(sessionId);
                
                // Use proper JSON serializer options with explicit handling of cycles and references
                var options = new JsonSerializerOptions { 
                    WriteIndented = true,  // Make JSON more readable for debugging
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve, // Handle circular references
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull // Skip null properties
                };
                
                try
                {
                    var json = JsonSerializer.Serialize(documentCopy, options);
                    
                    // Ensure the directory exists
                    var directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory) && directory != null)
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    File.WriteAllText(filePath, json);
                    
                    // Verify the file was written successfully
                    if (File.Exists(filePath))
                    {
                        var fileInfo = new FileInfo(filePath);
                        _logger.LogInformation(
                            "Document {FileName} successfully stored for session {SessionId} with {ChunkCount} chunks (File size: {FileSize} bytes)",
                            document.FileName, sessionId, document.Chunks.Count, fileInfo.Length);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to write document file for session {SessionId}", sessionId);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON serialization error for session {SessionId}: {Error}", 
                        sessionId, jsonEx.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing document for session {SessionId}", sessionId);
            }
        }
        
        /// <summary>
        /// Retrieves a document for a specific session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <returns>Document if found, null otherwise</returns>
        public DocumentInfo GetDocument(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Cannot retrieve document with null or empty session ID");
                return null;
            }
            
            _logger.LogInformation("GetDocument called for session ID: {SessionId}", sessionId);
            
            // Dump all session IDs we have in memory for debugging
            var sessionIds = string.Join(", ", _documentStore.Keys);
            _logger.LogInformation("Current sessions in memory: {SessionCount} - {SessionIds}", 
                _documentStore.Count, sessionIds);
                
            DocumentInfo? document = null;

            // Try to get from memory first
            if (_documentStore.TryGetValue(sessionId, out document) && document?.Chunks?.Count > 0)
            {
                _logger.LogInformation(
                    "Retrieved document from memory for session {SessionId}: {FileName} with {ChunkCount} chunks and {MetadataCount} metadata items", 
                    sessionId, document.FileName, document.Chunks.Count, document.ChunkMetadata?.Count ?? 0);
                return document;
            }
            else if (_documentStore.ContainsKey(sessionId))
            {
                _logger.LogWarning("Found document in memory for session {SessionId} but it has no chunks or is invalid", sessionId);
            }
            
            // Fall back to file-based store
            try
            {
                var filePath = GetDocumentFilePath(sessionId);
                if (File.Exists(filePath))
                {
                    _logger.LogInformation("Loading document from disk for session {SessionId}", sessionId);
                    var json = File.ReadAllText(filePath);
                    
                    if (string.IsNullOrEmpty(json))
                    {
                        _logger.LogWarning("Empty JSON file for session {SessionId}", sessionId);
                        return null;
                    }
                    
                    // Configure JSON options for proper deserialization - match serialization settings
                    var options = new JsonSerializerOptions { 
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve, // Handle circular references
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull // Skip null properties
                    };
                    
                    try
                    {
                        // First try to deserialize directly
                        document = JsonSerializer.Deserialize<DocumentInfo>(json, options);
                        
                        // Validate the document chunks
                        if (document == null || document.Chunks == null || document.Chunks.Count == 0)
                        {
                            _logger.LogError("CRITICAL ERROR: Deserialized document is invalid or has no chunks for session {SessionId}", sessionId);
                            
                            // Try to recover the document using a workaround for JSON serialization issues
                            try {
                                // Try to extract just the chunks using a custom approach
                                _logger.LogWarning("Attempting chunk recovery for session {SessionId}", sessionId);
                                
                                // First get the basic document info using more lenient options
                                var fallbackOptions = new JsonSerializerOptions { 
                                    PropertyNameCaseInsensitive = true,
                                    AllowTrailingCommas = true,
                                    ReadCommentHandling = JsonCommentHandling.Skip
                                };
                                
                                // Create a temporary document with recovered properties
                                var tempDoc = JsonSerializer.Deserialize<DocumentInfo>(json, fallbackOptions);
                                
                                if (tempDoc != null && !string.IsNullOrEmpty(tempDoc.FileName)) {
                                    // Basic document info recovered, now try to extract chunks
                                    var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
                                    
                                    // Try to find the chunks array in the JSON
                                    if (jsonDoc.RootElement.TryGetProperty("chunks", out var chunksElement) ||
                                        jsonDoc.RootElement.TryGetProperty("Chunks", out chunksElement)) {
                                        
                                        if (chunksElement.ValueKind == JsonValueKind.Array) {
                                            // Create a new document with the recovered chunks
                                            document = new DocumentInfo {
                                                FileName = tempDoc.FileName,
                                                UploadTime = tempDoc.UploadTime,
                                                Chunks = new List<string>(),
                                                ChunkMetadata = tempDoc.ChunkMetadata,
                                                EntityIndex = tempDoc.EntityIndex,
                                                Summary = tempDoc.Summary,
                                                TotalLength = tempDoc.TotalLength
                                            };
                                            
                                            // Extract each chunk from the array
                                            foreach (var chunk in chunksElement.EnumerateArray()) {
                                                if (chunk.ValueKind == JsonValueKind.String) {
                                                    document.Chunks.Add(chunk.GetString() ?? "");
                                                }
                                            }
                                            
                                            _logger.LogWarning("Recovered {ChunkCount} chunks using fallback method for {FileName}", 
                                                document.Chunks.Count, document.FileName);
                                        }
                                    }
                                }
                            } catch (Exception ex) {
                                _logger.LogError(ex, "Failed to recover document chunks for session {SessionId}", sessionId);
                            }
                            
                            // If recovery failed, return null
                            if (document == null || document.Chunks == null || document.Chunks.Count == 0) {
                                return null;
                            }
                        }
                        
                        // Cache it back in memory
                        _documentStore[sessionId] = document;
                        _logger.LogInformation(
                            "Successfully loaded document from disk for session {SessionId}: {FileName} with {ChunkCount} chunks",
                            sessionId, document.FileName, document.Chunks.Count);
                        
                        return document;
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON deserialization error for session {SessionId}: {Error}", 
                            sessionId, jsonEx.Message);
                        return null;
                    }
                }
                else
                {
                    _logger.LogWarning("No document file exists for session {SessionId}", sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document for session {SessionId}", sessionId);
            }
            
            _logger.LogWarning("No document found for session {SessionId}", sessionId);
            return null;
        }
        
        /// <summary>
        /// Removes a document for a specific session
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        public void ClearDocument(string sessionId)
        {
            // Remove from memory
            _documentStore.TryRemove(sessionId, out _);
            
            // Remove from disk
            try
            {
                var filePath = GetDocumentFilePath(sessionId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                _logger.LogInformation("Document cleared for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing document for session {SessionId}", sessionId);
            }
        }
        
        private string GetDocumentFilePath(string sessionId) => 
            Path.Combine(_persistencePath, $"{sessionId}.json");
            
        /// <summary>
        /// Stores a document for a specific session asynchronously
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="document">Document to store</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public async Task StoreDocumentAsync(string sessionId, DocumentInfo document)
        {
            // Call the synchronous method for now, but wrap in Task to make it async compatible
            // In a real implementation, you would rewrite the file operations to be properly async
            await Task.Run(() => StoreDocument(sessionId, document));
        }
        
        /// <summary>
        /// Retrieves a document for a specific session asynchronously
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <returns>Document if found, null otherwise</returns>
        public async Task<DocumentInfo?> GetDocumentAsync(string sessionId)
        {
            // Call the synchronous method for now, but wrap in Task to make it async compatible
            // In a real implementation, you would rewrite the file operations to be properly async
            return await Task.Run(() => GetDocument(sessionId));
        }
    }
}
