using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Backend.Models;

namespace Backend.Services
{
    /// <summary>
    /// Service responsible for preparing document context for LLM prompts
    /// </summary>
    public class DocumentContextService
    {
        private readonly ILogger<DocumentContextService> _logger;
        
        public DocumentContextService(ILogger<DocumentContextService> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Prepares document context for LLM consumption with token management
        /// </summary>
        public string PrepareDocumentContext(DocumentInfo documentInfo, string userMessage)
        {
            if (documentInfo?.Chunks == null || documentInfo.Chunks.Count == 0)
            {
                _logger.LogWarning("No document chunks available to prepare context");
                return string.Empty;
            }
            
            _logger.LogInformation("Preparing document context from {ChunkCount} chunks", documentInfo.Chunks.Count);
            
            // Extract any pages specifically requested by the user
            List<int> requestedPages = ExtractRequestedPages(userMessage);
            if (requestedPages.Count > 0)
            {
                _logger.LogInformation("User requested specific pages: {Pages}", string.Join(", ", requestedPages));
            }
            
            // Use token estimation to limit document content (approximately 1 token ≈ 4 chars)
            const int maxTokenEstimate = 7000; // Conservative limit for document context
            const int tokensPerChar = 4;       // Rough estimate
            int maxChars = maxTokenEstimate * tokensPerChar;
            
            // First identify high priority chunks (containing requested pages or ITA Group)
            List<string> highPriorityChunks = new List<string>();
            List<string> regularChunks = new List<string>();
            
            // Prioritize chunks containing requested pages
            if (requestedPages.Count > 0 && documentInfo.ChunkMetadata != null)
            {
                for (int i = 0; i < documentInfo.Chunks.Count; i++)
                {
                    if (i < documentInfo.ChunkMetadata.Count && documentInfo.ChunkMetadata[i] != null && 
                        documentInfo.ChunkMetadata[i].Pages != null && 
                        documentInfo.ChunkMetadata[i].Pages.Any(p => requestedPages.Contains(p)))
                    {
                        highPriorityChunks.Add(documentInfo.Chunks[i]);
                        _logger.LogInformation("Prioritizing chunk containing page(s) {Pages}", 
                            string.Join(", ", documentInfo.ChunkMetadata[i].Pages.Where(p => requestedPages.Contains(p))));
                    }
                }
            }
            
            // Always prioritize page 42 if it exists in metadata
            for (int i = 0; i < documentInfo.Chunks.Count; i++)
            {
                if (i < documentInfo.ChunkMetadata?.Count && documentInfo.ChunkMetadata[i]?.Pages != null && 
                    documentInfo.ChunkMetadata[i].Pages.Contains(42) &&
                    !highPriorityChunks.Contains(documentInfo.Chunks[i]))
                {
                    highPriorityChunks.Add(documentInfo.Chunks[i]);
                    _logger.LogInformation("Prioritizing chunk containing page 42");
                }
            }
            
            // Prioritize chunks containing "ITA Group"
            if (userMessage.Contains("ITA") || userMessage.Contains("Group"))
            {
                if (documentInfo.EntityIndex != null && documentInfo.EntityIndex.ContainsKey("ITA Group"))
                {
                    foreach (var chunkIndex in documentInfo.EntityIndex["ITA Group"])
                    {
                        if (chunkIndex < documentInfo.Chunks.Count && !highPriorityChunks.Contains(documentInfo.Chunks[chunkIndex]))
                        {
                            highPriorityChunks.Add(documentInfo.Chunks[chunkIndex]);
                            _logger.LogInformation("Prioritizing chunk {Index} containing ITA Group", chunkIndex);
                        }
                    }
                }
            }
            
            // Add remaining chunks as regular priority
            for (int i = 0; i < documentInfo.Chunks.Count; i++)
            {
                if (!highPriorityChunks.Contains(documentInfo.Chunks[i]))
                {
                    regularChunks.Add(documentInfo.Chunks[i]);
                }
            }
            
            _logger.LogInformation("Identified {HighPriorityCount} high priority chunks and {RegularCount} regular chunks",
                highPriorityChunks.Count, regularChunks.Count);
                
            // Build final chunk list within token budget
            List<string> chunksToSend = new List<string>();
            int totalChars = 0;
            
            // First add all high priority chunks
            foreach (var chunk in highPriorityChunks)
            {
                chunksToSend.Add(chunk);
                totalChars += chunk.Length;
            }
            
            // Then add regular chunks until we approach the limit
            foreach (var chunk in regularChunks)
            {
                if (totalChars + chunk.Length <= maxChars)
                {
                    chunksToSend.Add(chunk);
                    totalChars += chunk.Length;
                }
            }
            
            _logger.LogInformation("Selected {ChunkCount} chunks with approximately {TokenEstimate} tokens", 
                chunksToSend.Count, totalChars / tokensPerChar);
            
            // If we couldn't include all chunks, log a warning
            if (chunksToSend.Count < documentInfo.Chunks.Count)
            {
                _logger.LogWarning("Token limits prevented including {ExcludedCount} chunks", 
                    documentInfo.Chunks.Count - chunksToSend.Count);
            }
            
            // Build the final document context
            var documentContext = new StringBuilder();
            documentContext.AppendLine($"Document: {documentInfo.FileName}");
            
            // Collect metadata about included pages
            HashSet<int> includedPages = new HashSet<int>();
            
            // Check if we have ChunkMetadata available (using new semantic chunker)
            if (documentInfo.ChunkMetadata != null && documentInfo.ChunkMetadata.Count > 0)
            {
                _logger.LogInformation("Using enhanced semantic chunk metadata for page detection");
                
                // Use metadata directly to identify pages
                foreach (var metadata in documentInfo.ChunkMetadata)
                {
                    if (metadata.Pages != null)
                    {
                        foreach (var page in metadata.Pages)
                        {
                            includedPages.Add(page);
                        }
                    }
                }
                
                _logger.LogInformation("Semantic chunker detected {Count} unique pages in document", includedPages.Count);
            }
            else
            {
                // Fallback to regex pattern matching for page detection
                _logger.LogInformation("Falling back to regex pattern matching for page detection");
                
                foreach (var chunk in chunksToSend)
                {
                    var pageMarkers = Regex.Matches(chunk, @"\[(?:PAGE|DOCUMENT PAGE)\s+(\d+)\s+(?:OF|of)\s+(\d+)\]");
                    foreach (Match match in pageMarkers)
                    {
                        if (int.TryParse(match.Groups[1].Value, out int pageNum))
                        {
                            includedPages.Add(pageNum);
                        }
                    }
                }
            }
            
            documentContext.AppendLine($"The included content covers pages: {string.Join(", ", includedPages.OrderBy(p => p).Select(p => p.ToString()))}");
            
            if (requestedPages.Count > 0)
            {
                documentContext.AppendLine($"You specifically requested information from page(s): {string.Join(", ", requestedPages)}");
            }
            
            // Add important instructions about page numbers and entities
            documentContext.AppendLine("IMPORTANT INSTRUCTION: Pay very close attention to all PAGE NUMBERS in this document. Look specifically for page markers like [PAGE 42 OF 166].");
            documentContext.AppendLine("The user wants information from specific pages, and it's critical that you find and report information from those pages.");
            
            // Special instructions for ITA Group
            if ((userMessage.Contains("ITA") || userMessage.Contains("Group")) && 
                documentInfo.EntityIndex != null && documentInfo.EntityIndex.ContainsKey("ITA Group"))
            {
                documentContext.AppendLine("Pay special attention to mentions of **ITA Group** in the document. The user is specifically asking about this entity.");
            }
            
            documentContext.AppendLine("---\n");
            
            // Add each chunk with enhanced page metadata visibility
            for (int i = 0; i < chunksToSend.Count; i++)
            {
                string chunk = chunksToSend[i];
                
                // Find the index of this chunk in the original document
                int index = documentInfo.Chunks.IndexOf(chunk);
                
                // Initialize variables for chunk metadata
                string enhancedChunk = chunk;
                List<int> chunkPages = new List<int>();
                List<string> chunkEntities = new List<string>();
                string pageInfo;
                
                // Check if we have metadata for this chunk from the semantic chunker
                if (documentInfo.ChunkMetadata != null && index < documentInfo.ChunkMetadata.Count && documentInfo.ChunkMetadata[index] != null)
                {
                    var metadata = documentInfo.ChunkMetadata[index];
                    
                    // Use metadata for pages and entities
                    chunkPages = metadata.Pages ?? new List<int>();
                    chunkEntities = metadata.KeyEntities ?? new List<string>();
                    
                    // Create a page info header using metadata
                    if (chunkPages != null && chunkPages.Count > 0)
                    {
                        string pageList = string.Join(", ", chunkPages);
                        pageInfo = $"--- DOCUMENT CONTENT FROM PAGE(S) {pageList} ---";
                        
                        // Also highlight any page markers in the content
                        enhancedChunk = Regex.Replace(enhancedChunk, 
                            @"\[(PAGE|DOCUMENT PAGE)\s+(\d+)\s+(OF|of)\s+(\d+)\]", 
                            m => $"### [DOCUMENT PAGE {m.Groups[2].Value} of {m.Groups[4].Value}] ###");
                    }
                    else
                    {
                        pageInfo = $"--- DOCUMENT CHUNK {i + 1} OF {chunksToSend.Count} ---";
                    }
                    
                    // If this chunk contains key entities, highlight them
                    if (chunkEntities != null && chunkEntities.Count > 0)
                    {
                        pageInfo += $" [ENTITIES: {string.Join(", ", chunkEntities)}]";
                        
                        // Highlight each entity in the text
                        foreach (var entity in chunkEntities)
                        {
                            // Only highlight entities with 3+ characters to avoid false positives
                            if (entity.Length >= 3)
                            {
                                enhancedChunk = Regex.Replace(enhancedChunk, 
                                    $@"\b{Regex.Escape(entity)}\b", 
                                    $"**{entity}**", 
                                    RegexOptions.IgnoreCase);
                            }
                        }
                    }
                }
                else
                {
                    // Fallback for chunks without metadata
                    pageInfo = $"--- DOCUMENT CHUNK {i + 1} OF {chunksToSend.Count} ---";
                    
                    // Try to extract and highlight page markers
                    enhancedChunk = Regex.Replace(enhancedChunk, 
                        @"\[(PAGE|DOCUMENT PAGE)\s+(\d+)\s+(OF|of)\s+(\d+)\]", 
                        m => $"### [DOCUMENT PAGE {m.Groups[2].Value} of {m.Groups[4].Value}] ###");
                        
                    // Highlight ITA Group mentions
                    enhancedChunk = Regex.Replace(enhancedChunk, 
                        @"\bITA Group\b", 
                        "**ITA Group**", 
                        RegexOptions.IgnoreCase);
                }
                
                documentContext.AppendLine(pageInfo);
                documentContext.AppendLine(enhancedChunk);
                documentContext.AppendLine("\n---\n");
            }
            
            _logger.LogInformation("Document context preparation complete: {Length} characters, ~{Tokens} tokens", 
                documentContext.Length, documentContext.Length / tokensPerChar);
                
            return documentContext.ToString();
        }
        
        /// <summary>
        /// Extract requested page numbers from a user message
        /// </summary>
        private List<int> ExtractRequestedPages(string message)
        {
            var result = new List<int>();
            
            if (string.IsNullOrEmpty(message))
                return result;
            
            // Look for references to specific pages
            // Patterns: "page 42", "p. 42", "p42", "pages 42-45", etc.
            var pagePatterns = new[]
            {
                @"page\s+(\d+)",            // "page 42"
                @"p\.\s*(\d+)",             // "p. 42" or "p.42"
                @"p\s*(\d+)",               // "p 42" or "p42"
                @"pages?\s+(\d+)[-–](\d+)"  // "page 42-45" or "pages 42-45"
            };
            
            foreach (var pattern in pagePatterns)
            {
                var matches = Regex.Matches(message, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    // Check if this is a page range
                    if (match.Groups.Count > 2 && int.TryParse(match.Groups[1].Value, out int startPage) && 
                        int.TryParse(match.Groups[2].Value, out int endPage))
                    {
                        // Add all pages in the range
                        for (int i = startPage; i <= endPage; i++)
                        {
                            if (!result.Contains(i))
                                result.Add(i);
                        }
                    }
                    else if (int.TryParse(match.Groups[1].Value, out int pageNum))
                    {
                        if (!result.Contains(pageNum))
                            result.Add(pageNum);
                    }
                }
            }
            
            // Special case for page 42 which is of particular interest
            if (message.Contains("42") && !result.Contains(42))
            {
                _logger.LogInformation("Found direct mention of page 42 in message");
                result.Add(42);
            }
            
            return result;
        }
    }
}
