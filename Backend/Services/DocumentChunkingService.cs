using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Backend.Services.Interfaces;

namespace Backend.Services
{
    public class DocumentChunkingService : IDocumentChunkingService
    {
        private readonly ILogger<DocumentChunkingService> _logger;
        
        public DocumentChunkingService(ILogger<DocumentChunkingService> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Splits a document into chunks of approximately the specified size
        /// </summary>
        /// <param name="text">The document text to chunk</param>
        /// <param name="maxChunkSize">Maximum size of each chunk in characters</param>
        /// <returns>List of document chunks</returns>
        public List<string> ChunkDocument(string text, int maxChunkSize = 500)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    _logger.LogWarning("Attempted to chunk empty or null text");
                    return new List<string>();
                }
            
            // If the text is smaller than the max chunk size, return it as a single chunk
            if (text.Length <= maxChunkSize)
            {
                return new List<string> { text };
            }
            
            // Determine document size category for adaptive chunking
            bool isLargeDocument = text.Length > 100000;
            bool isVeryLargeDocument = text.Length > 300000;
            
            // Adjust chunk size based on document size
            // For very large docs, use smaller chunks to ensure more content fits in the context window
            int adjustedChunkSize;
            if (isVeryLargeDocument) {
                adjustedChunkSize = 150; // Much smaller for very large docs
                _logger.LogInformation("Very large document: Using micro chunk size of {ChunkSize} characters to maximize document coverage", adjustedChunkSize);
            } else if (isLargeDocument) {
                adjustedChunkSize = 200; // Smaller for large docs
                _logger.LogInformation("Large document: Using tiny chunk size of {ChunkSize} characters to maximize document coverage", adjustedChunkSize);
            } else {
                adjustedChunkSize = maxChunkSize;
                _logger.LogInformation("Standard document: Using small chunk size of {ChunkSize} characters to maximize document coverage", adjustedChunkSize);
            }
            
            _logger.LogInformation("Document chunking: Size={Length} chars, Category={Category}, ChunkSize={ChunkSize}", 
                text.Length, 
                isVeryLargeDocument ? "Very Large" : (isLargeDocument ? "Large" : "Standard"),
                adjustedChunkSize);
                
            // Check if the document contains page markers to estimate page count
            var pageMarkers = Regex.Matches(text, @"\[PAGE \d+ OF \d+\]");
            if (pageMarkers.Count > 0)
            {
                _logger.LogInformation("Document appears to contain {Count} page markers", pageMarkers.Count);
                
                // Try to extract the total page count from the last page marker
                var lastMarker = pageMarkers[pageMarkers.Count - 1].Value;
                var match = Regex.Match(lastMarker, @"\[PAGE \d+ OF (\d+)\]");
                if (match.Success)
                {
                    int totalPages = int.Parse(match.Groups[1].Value);
                    _logger.LogInformation("Document has approximately {Pages} total pages", totalPages);
                }
            }
            
            var chunks = new List<string>();
            
            // First, check if the text contains page markers from our PDF extraction
            var pageMarkerRegex = new Regex(@"\[PAGE\s+(\d+)\s+OF\s+(\d+)\]", RegexOptions.Multiline);
            var hasPageMarkers = pageMarkerRegex.IsMatch(text);
            
            if (hasPageMarkers)
            {
                _logger.LogInformation("Document contains page markers, using page-aware chunking");
                // Use page-aware chunking
                chunks = ChunkByPages(text, adjustedChunkSize);
            }
            else
            {
                _logger.LogInformation("Document does not contain page markers, using standard chunking");
                // Use the standard chunking approach
                chunks = ChunkByParagraphs(text, adjustedChunkSize);
            }
            
            // For very large documents, add additional metadata to chunks to improve searchability
            if (isLargeDocument && chunks.Count > 20)
            {
                chunks = EnhanceChunksWithMetadata(chunks);
            }
            
            _logger.LogInformation("Document chunked into {ChunkCount} chunks", chunks.Count);
            return chunks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error chunking document: {Message}", ex.Message);
                // Return at least one chunk to avoid null errors
                return new List<string> { "Error occurred while processing document. Please try again with a different file." };
            }
        }
        
        /// <summary>
        /// Chunks document by preserving page markers with enhanced page tracking
        /// </summary>
        private List<string> ChunkByPages(string text, int maxChunkSize)
        {
            var chunks = new List<string>();
            var pageMarkerRegex = new Regex(@"\[PAGE\s+(\d+)\s+OF\s+(\d+)\]([\s\S]*?)(?=\[PAGE|$)", RegexOptions.Multiline);
            
            // Track which pages are included in our chunks
            var includedPages = new HashSet<int>();
            int totalPagesInDoc = 0;
            
            // Find all page sections
            var matches = pageMarkerRegex.Matches(text);
            _logger.LogInformation("Found {Count} page sections in document", matches.Count);
            
            // For very large documents, use an even smaller chunk size to ensure more pages fit
            int pageChunkSize = matches.Count > 100 ? (maxChunkSize / 2) : maxChunkSize;
            _logger.LogInformation("Using page chunk size of {ChunkSize} characters", pageChunkSize);
            
            foreach (Match match in matches)
            {
                int pageNumber = int.Parse(match.Groups[1].Value);
                int totalPages = int.Parse(match.Groups[2].Value);
                totalPagesInDoc = totalPages; // Save for later reporting
                
                string pageContent = match.Groups[3].Value.Trim();
                string pageMarker = $"[PAGE {pageNumber} OF {totalPages}]";
                
                // Add this page to our tracking
                includedPages.Add(pageNumber);
                
                // Include page info in a metadata header that's always visible
                string pageMetadata = $"[DOCUMENT PAGE {pageNumber} of {totalPages}]";
                
                // If this page content is small enough, add it as a single chunk
                if (pageContent.Length + pageMarker.Length + pageMetadata.Length <= pageChunkSize)
                {
                    chunks.Add($"{pageMetadata}\n{pageMarker}\n\n{pageContent}");
                    _logger.LogDebug("Added page {PageNumber} as a single chunk", pageNumber);
                }
                else
                {
                    // Otherwise, split this page into smaller chunks while preserving the page marker
                    int effectiveChunkSize = pageChunkSize - pageMetadata.Length - pageMarker.Length - 15; 
                    var pageChunks = ChunkByParagraphs(pageContent, effectiveChunkSize);
                    
                    _logger.LogDebug("Split page {PageNumber} into {ChunkCount} smaller chunks", pageNumber, pageChunks.Count);
                    
                    for (int i = 0; i < pageChunks.Count; i++)
                    {
                        string chunkMetadata = $"{pageMetadata} (Part {i+1}/{pageChunks.Count})";
                        chunks.Add($"{chunkMetadata}\n{pageMarker}\n\n{pageChunks[i]}");
                    }
                }
            }
            
            // Log statistics about included pages
            _logger.LogInformation("Document chunking included {IncludedCount} of {TotalPages} pages", 
                includedPages.Count, totalPagesInDoc);
                
            // Check for missing pages
            if (includedPages.Count < totalPagesInDoc)
            {
                var missingPages = Enumerable.Range(1, totalPagesInDoc).Where(p => !includedPages.Contains(p));
                _logger.LogWarning("Pages not included in chunks: {MissingPages}", string.Join(", ", missingPages));
            }
            
            return chunks;
        }
        
        /// <summary>
        /// Standard chunking by paragraphs and sentences
        /// </summary>
        private List<string> ChunkByParagraphs(string text, int maxChunkSize)
        {
            var chunks = new List<string>();
            
            // Split the text into paragraphs
            var paragraphs = Regex.Split(text, @"(\r?\n){2,}").Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            
            var currentChunk = new StringBuilder();
            var currentChunkPages = new HashSet<int>(); // Track pages in current chunk
            int currentChunkStartPage = 0;
            int currentChunkEndPage = 0;
            
            // Split on paragraphs first
            var contentParagraphs = Regex.Split(text, @"(\r?\n\r?\n)");
            
            foreach (var paragraph in contentParagraphs)
            {   
                // Check if this paragraph contains a page marker
                var pageMatch = Regex.Match(paragraph, @"\[PAGE (\d+) OF \d+\]");
                if (pageMatch.Success)
                {
                    int pageNumber = int.Parse(pageMatch.Groups[1].Value);
                    currentChunkPages.Add(pageNumber);
                    
                    // Update chunk start/end page
                    if (currentChunkStartPage == 0 || pageNumber < currentChunkStartPage)
                        currentChunkStartPage = pageNumber;
                    if (pageNumber > currentChunkEndPage)
                        currentChunkEndPage = pageNumber;
                }
                
                // If adding this paragraph would exceed the chunk size, start a new chunk
                if (currentChunk.Length + paragraph.Length > maxChunkSize && currentChunk.Length > 0)
                {
                    // Add page range metadata to the chunk
                    if (currentChunkStartPage > 0 && currentChunkEndPage > 0)
                    {
                        string pageInfo = $"[Chunk contains pages {currentChunkStartPage}-{currentChunkEndPage}]\n";
                        string chunkWithMeta = pageInfo + currentChunk.ToString().Trim();
                        chunks.Add(chunkWithMeta);
                        
                        _logger.LogDebug("Created document chunk of {Length} chars containing pages {StartPage}-{EndPage}", 
                            chunkWithMeta.Length, currentChunkStartPage, currentChunkEndPage);
                    }
                }
                else
                {
                    currentChunk.AppendLine(paragraph);
                    currentChunk.AppendLine(); // Add a blank line between paragraphs
                }
            }
            
            // Add the last chunk if it's not empty
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }
            
            return chunks;
        }
        
        /// <summary>
        /// Enhances chunks with additional metadata to improve searchability
        /// </summary>
        public List<string> EnhanceChunksWithMetadata(List<string> chunks)
        {
            var enhancedChunks = new List<string>();
            int chunkIndex = 0;
            int totalChunks = chunks.Count;
            
            foreach (var chunk in chunks)
            {
                var sb = new StringBuilder();
                
                // Extract location context from chunk
                string locationContext = "";
                
                // Try to extract page information if present (check both formats)
                var pageMatch = Regex.Match(chunk, @"\[DOCUMENT PAGE (\d+) of (\d+)\]");
                if (!pageMatch.Success)
                {
                    pageMatch = Regex.Match(chunk, @"\[PAGE (\d+) OF (\d+)\]");
                }
                
                if (pageMatch.Success)
                {
                    string pageNum = pageMatch.Groups[1].Value;
                    string totalPages = pageMatch.Groups[2].Value;
                    locationContext = $"Page {pageNum} of {totalPages}";
                    
                    // Check if this is a partial page chunk
                    var partMatch = Regex.Match(chunk, @"\[DOCUMENT PAGE \d+ of \d+\] \(Part (\d+)/(\d+)\)");
                    if (partMatch.Success)
                    {
                        string partNum = partMatch.Groups[1].Value;
                        string totalParts = partMatch.Groups[2].Value;
                        locationContext += $" (Part {partNum} of {totalParts})";
                    }
                }
                else
                {
                    // If no page marker, provide position in document
                    int positionPercentage = (chunkIndex * 100) / totalChunks;
                    if (positionPercentage < 33)
                    {
                        locationContext = "Beginning section of document";
                    }
                    else if (positionPercentage < 66)
                    {
                        locationContext = "Middle section of document";
                    }
                    else
                    {
                        locationContext = "End section of document";
                    }
                }
                
                // Try to identify content type (table, list, paragraph)
                string contentType = "text";
                if (Regex.IsMatch(chunk, @"[\t|]{2,}"))
                {
                    contentType = "table";
                }
                else if (Regex.IsMatch(chunk, @"(^|\n)[\*\-\d]+\."))
                {
                    contentType = "list";
                }
                
                // Add metadata header
                sb.AppendLine($"[METADATA] Location: {locationContext} | Type: {contentType} | Index: {chunkIndex+1}/{totalChunks}");
                sb.AppendLine();
                
                // Add the original chunk content
                sb.Append(chunk);
                
                enhancedChunks.Add(sb.ToString());
                chunkIndex++;
            }
            
            _logger.LogInformation("Enhanced {ChunkCount} chunks with metadata", enhancedChunks.Count);
            return enhancedChunks;
        }

        /// <summary>
        /// Splits text into sentences
        /// </summary>
        private List<string> SplitIntoSentences(string text)
        {
            // Simple sentence splitting - this could be improved with NLP libraries
            var sentenceRegex = new Regex(@"(\.|\?|\!)\s+");
            var sentences = sentenceRegex.Split(text)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select((s, i) => i % 2 == 0 ? s : s + " ") // Add back the punctuation
                .ToList();
                
            var result = new List<string>();
            var currentSentence = new StringBuilder();
            
            foreach (var part in sentences)
            {
                currentSentence.Append(part);
                
                // If this part ends with punctuation, add it to the result
                if (part.EndsWith(". ") || part.EndsWith("? ") || part.EndsWith("! "))
                {
                    result.Add(currentSentence.ToString());
                    currentSentence.Clear();
                }
            }
            
            // Add any remaining text
            if (currentSentence.Length > 0)
            {
                result.Add(currentSentence.ToString());
            }
            
            return result;
        }
    }
}
