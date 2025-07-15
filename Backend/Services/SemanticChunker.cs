using System.Text.RegularExpressions;
using Backend.Models;
using System.Text;
using Backend.Services.Interfaces;

namespace Backend.Services
{
    /// <summary>
    /// SemanticChunker implements LangChain-inspired chunking strategies for improved document processing
    /// </summary>
    public class SemanticChunker : ISemanticChunker
    {
        private readonly ILogger<SemanticChunker> _logger;
        
        public SemanticChunker(ILogger<SemanticChunker> logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Process a document with advanced semantic chunking inspired by LangChain
        /// </summary>
        public DocumentInfo ProcessDocument(string content, string fileName)
        {
            _logger.LogInformation("Processing document {FileName} with semantic chunking - size: {Size} chars", fileName, content?.Length ?? 0);
            
            if (string.IsNullOrEmpty(content))
            {
                _logger.LogWarning("Empty content provided for semantic chunking");
                return new DocumentInfo { FileName = fileName };
            }
            
            var docInfo = new DocumentInfo
            {
                FileName = fileName,
                TotalLength = content.Length,
                UploadTime = DateTime.UtcNow
            };
            
            // STEP 1: Extract special entities we want to track
            var specialEntities = ExtractSpecialEntities(content);
            _logger.LogInformation("Found {Count} special entities in document", specialEntities.Count);
            
            // STEP 2: Create micro-chunks to maximize context inclusion
            var chunks = CreateChunks(content);
            docInfo.Chunks = chunks;
            _logger.LogInformation("Created {Count} semantic chunks from document", chunks.Count);
            
            // STEP 3: Add metadata to each chunk and build entity index
            EnrichChunkMetadata(docInfo, content, specialEntities);
            _logger.LogInformation("Enhanced chunks with metadata and built entity index");
            
            // STEP 4: Generate document summary
            docInfo.Summary = GenerateDocumentSummary(docInfo);
            
            return docInfo;
        }
        
        /// <summary>
        /// Extract special entities like "ITA Group" that we want to specifically track
        /// </summary>
        private HashSet<string> ExtractSpecialEntities(string content)
        {
            var entities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Special case for ITA Group which we know is important
            if (content.Contains("ITA Group", StringComparison.OrdinalIgnoreCase))
            {
                entities.Add("ITA Group");
                _logger.LogInformation("Found specific entity of interest: ITA Group");
            }
            
            // Look for other potential entities that might be important
            // This is a simplified approach - in production you would use NER or other NLP techniques
            var potentialCompanies = Regex.Matches(content, @"\b([A-Z][A-Za-z]+\s+(?:Group|Inc\.?|LLC|Corporation|Company|Co\.?|Ltd\.?))");
            foreach (Match match in potentialCompanies)
            {
                entities.Add(match.Groups[1].Value.Trim());
            }
            
            // Extract numbers and values that might be important (e.g., square footage)
            var numberValues = Regex.Matches(content, @"\b(\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+\.\d+)\s+(sq(?:\.|\s)(?:ft|foot|feet)|SF)\b", RegexOptions.IgnoreCase);
            foreach (Match match in numberValues)
            {
                entities.Add(match.Value.Trim());
            }
            
            return entities;
        }
        
        /// <summary>
        /// Create micro-chunks from document content
        /// </summary>
        private List<string> CreateChunks(string content)
        {
            var chunks = new List<string>();
            
            // Use a very small chunk size to ensure we can fit the entire document
            int chunkSize = 150;  
            
            // Detect if we need an even smaller chunk size for very large documents
            if (content.Length > 300000)
            {
                chunkSize = 100; // Even smaller for very large documents
                _logger.LogInformation("Very large document detected - using tiny chunk size of {Size}", chunkSize);
            }
            
            // Identify page markers throughout the document for better chunking
            var pageMarkers = Regex.Matches(content, @"\[(?:PAGE|DOCUMENT PAGE)\s+(\d+)\s+(?:OF|of)\s+(\d+)\]");
            
            // Preserve paragraphs and page breaks in chunking
            // Split by double newlines (paragraphs) first to respect content structure
            var paragraphs = Regex.Split(content, @"(\r?\n){2,}");
            
            // Process each paragraph
            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                    continue;
                    
                // If paragraph contains a page marker, prioritize keeping it intact if possible
                if (Regex.IsMatch(paragraph, @"\[(?:PAGE|DOCUMENT PAGE)\s+\d+\s+(?:OF|of)\s+\d+\]"))
                {
                    // Try to keep the entire paragraph with page marker together
                    if (paragraph.Length <= chunkSize * 1.5)  // Allow slightly larger chunks for page markers
                    {
                        chunks.Add(paragraph.Trim());
                        continue;
                    }
                }
                
                // For longer paragraphs, break them into smaller chunks
                var words = paragraph.Split(' ');
                var currentChunk = new StringBuilder();
                
                foreach (var word in words)
                {
                    // If adding this word would exceed chunk size, save current chunk and start a new one
                    if (currentChunk.Length + word.Length + 1 > chunkSize && currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }
                    
                    if (currentChunk.Length > 0)
                        currentChunk.Append(' ');
                        
                    currentChunk.Append(word);
                }
                
                // Add any remaining content as a chunk
                if (currentChunk.Length > 0)
                    chunks.Add(currentChunk.ToString().Trim());
            }
            
            return chunks;
        }
        
        /// <summary>
        /// Enrich chunks with metadata and build entity index
        /// </summary>
        private void EnrichChunkMetadata(DocumentInfo docInfo, string originalContent, HashSet<string> specialEntities)
        {
            // Create chunk metadata and entity index
            int position = 0;
            for (int i = 0; i < docInfo.Chunks.Count; i++)
            {
                var chunk = docInfo.Chunks[i];
                
                // Find where this chunk appears in the original document
                int chunkStartPosition = originalContent.IndexOf(chunk, position);
                if (chunkStartPosition == -1) chunkStartPosition = position; // Fallback
                int chunkEndPosition = chunkStartPosition + chunk.Length;
                position = chunkEndPosition; // Update position for next search
                
                // Create metadata for this chunk
                var metadata = new ChunkMetadata
                {
                    ChunkIndex = i,
                    StartPosition = chunkStartPosition,
                    EndPosition = chunkEndPosition
                };
                
                // Extract any page numbers from the chunk
                var pageMatches = Regex.Matches(chunk, @"\[(?:PAGE|DOCUMENT PAGE)\s+(\d+)\s+(?:OF|of)\s+(\d+)\]");
                foreach (Match match in pageMatches)
                {
                    if (int.TryParse(match.Groups[1].Value, out int pageNumber))
                    {
                        metadata.Pages.Add(pageNumber);
                        
                        // Log if we found page 42
                        if (pageNumber == 42)
                        {
                            _logger.LogInformation("Found PAGE 42 in chunk {Index}: {Preview}", 
                                i, chunk.Length > 50 ? chunk.Substring(0, 50) + "..." : chunk);
                        }
                    }
                }
                
                // Check if chunk contains special entities
                foreach (var entity in specialEntities)
                {
                    if (chunk.Contains(entity, StringComparison.OrdinalIgnoreCase))
                    {
                        metadata.KeyEntities.Add(entity);
                        
                        // Add to entity index for quick retrieval
                        if (!docInfo.EntityIndex.ContainsKey(entity))
                        {
                            docInfo.EntityIndex[entity] = new List<int>();
                        }
                        
                        docInfo.EntityIndex[entity].Add(i);
                        
                        // Log if we found ITA Group
                        if (entity.Equals("ITA Group", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Found ITA Group in chunk {Index}: {Preview}", 
                                i, chunk.Length > 50 ? chunk.Substring(0, 50) + "..." : chunk);
                        }
                    }
                }
                
                docInfo.ChunkMetadata.Add(metadata);
            }
        }
        
        /// <summary>
        /// Generate a simple document summary with key statistics
        /// </summary>
        private string GenerateDocumentSummary(DocumentInfo docInfo)
        {
            var summary = new StringBuilder();
            
            // Create a summary of the document's structure
            summary.AppendLine($"Document: {docInfo.FileName}");
            summary.AppendLine($"Total Length: {docInfo.TotalLength} characters");
            summary.AppendLine($"Chunks: {docInfo.Chunks.Count}");
            
            // Summarize page coverage
            var allPages = docInfo.ChunkMetadata
                .SelectMany(m => m.Pages)
                .Distinct()
                .OrderBy(p => p)
                .ToList();
                
            summary.AppendLine($"Page Coverage: {allPages.Count} pages");
            summary.AppendLine($"Pages: {string.Join(", ", allPages)}");
            
            // Summarize entity coverage
            summary.AppendLine($"Key Entities: {docInfo.EntityIndex.Count}");
            foreach (var entity in docInfo.EntityIndex.Keys)
            {
                int chunkCount = docInfo.EntityIndex[entity].Count;
                summary.AppendLine($"  - {entity}: {chunkCount} mentions");
            }
            
            return summary.ToString();
        }
    }
}
