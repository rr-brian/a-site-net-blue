using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Backend.Models;
using Backend.Services.Interfaces;

namespace Backend.Services
{
    public class DocumentSearchService : IDocumentSearchService
    {
        private readonly ILogger<DocumentSearchService> _logger;

        public DocumentSearchService(ILogger<DocumentSearchService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Finds the most relevant chunks for a given query
        /// </summary>
        public List<string> FindRelevantChunks(DocumentInfo documentInfo, string query)
        {
            if (documentInfo == null || documentInfo.Chunks == null || !documentInfo.Chunks.Any())
            {
                _logger.LogWarning("No document chunks available for search");
                return new List<string>();
            }
            
            List<string> relevantChunks;
            
            // Check if the query mentions a specific page
            var pageMatch = Regex.Match(query, @"page\s*(\d+)", RegexOptions.IgnoreCase);
            int pageNumber = 0;
            if (pageMatch.Success && int.TryParse(pageMatch.Groups[1].Value, out pageNumber))
            {
                _logger.LogInformation("User is asking about page {PageNumber}", pageNumber);
                
                // Find chunks that contain the requested page
                relevantChunks = documentInfo.Chunks
                    .Where(chunk => chunk.Contains($"[PAGE {pageNumber} OF") || 
                            chunk.Contains($"Page {pageNumber}") || 
                            chunk.Contains($"page {pageNumber}"))
                    .ToList();
                    
                if (relevantChunks.Count == 0)
                {
                    _logger.LogWarning("No chunks found for page {PageNumber}", pageNumber);
                    // If no exact page match, include some surrounding pages as fallback
                    relevantChunks = documentInfo.Chunks
                        .Where(chunk => Regex.IsMatch(chunk, $@"\[PAGE\s*({pageNumber-2}|{pageNumber-1}|{pageNumber}|{pageNumber+1}|{pageNumber+2})\s*OF"))
                        .Take(8) // Increased from 5 to 8 for better context with large files
                        .ToList();
                }
            }
            else
            {
                // Use keyword-based semantic search for all queries
                var keywords = ExtractKeywords(query);
                _logger.LogInformation("Extracted keywords: {Keywords}", string.Join(", ", keywords));
                
                // For large documents, use more aggressive keyword matching
                bool isLargeDocument = documentInfo.Chunks.Count > 100;
                // Increase minimum chunk count to ensure better coverage
                int minRelevantChunks = isLargeDocument ? 12 : 8;
                
                // Log document size info
                _logger.LogInformation("Document contains {ChunkCount} chunks. Treating as {Size} document", 
                    documentInfo.Chunks.Count, isLargeDocument ? "large" : "standard");
                
                // Score chunks based on keyword matches with additional weight for keyword proximity
                var scoredChunks = documentInfo.Chunks
                    .Select(chunk => 
                    {
                        // Case-insensitive version of the chunk for searching
                        string lowerChunk = chunk.ToLower();
                        
                        // Base score is the sum of keyword matches with priority to multi-word keywords
                        int baseScore = keywords.Sum(keyword => {
                            string lowerKeyword = keyword.ToLower();
                            
                            // Direct match score
                            int directMatches = Regex.Matches(lowerChunk, lowerKeyword).Count;
                            
                            // Enhanced matching for entity names and companies
                            bool isEntityOrCompany = keyword.Contains(" ") || 
                                                    (keyword.Length >= 3 && char.IsUpper(keyword[0]));
                            
                            // Fuzzy matching for company/entity names
                            int fuzzyMatches = 0;
                            if (isEntityOrCompany) {
                                // Try variations of the keyword (with punctuation, prefixes/suffixes)
                                fuzzyMatches += Regex.Matches(lowerChunk, lowerKeyword.Replace(" ", "[-_ ]*")).Count;
                                fuzzyMatches += Regex.Matches(lowerChunk, $"{lowerKeyword}[\\.,\\s]+(inc|llc|co|ltd|corp)?").Count;
                                fuzzyMatches += Regex.Matches(lowerChunk, $"([a-z]+ )?{lowerKeyword}").Count;
                            }
                            
                            // Combined score with weights
                            int count = directMatches + (fuzzyMatches > 0 ? 1 : 0);
                            
                            // Give higher score to multi-word keywords and entities (like company names)
                            if (isEntityOrCompany && count > 0) {
                                return count * 4; // Quadruple score for entity/company matches
                            }
                            return count;
                        });
                        
                        // Additional score for chunks with multiple keywords close together
                        int proximityScore = 0;
                        if (keywords.Count >= 2)
                        {
                            // Check if chunk contains multiple keywords
                            int keywordsPresent = keywords.Count(keyword => 
                                lowerChunk.Contains(keyword.ToLower()));
                                
                            if (keywordsPresent >= 2)
                            {
                                proximityScore = keywordsPresent * 2; // Bonus for multiple keywords
                            }
                        }
                        
                        return new {
                            Chunk = chunk,
                            Score = baseScore + proximityScore
                        };
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();
                
                // Debug log for key entities
                foreach (var entity in keywords.Where(k => k.Contains("ita") || k.Contains("group")))
                {
                    var matchingChunks = scoredChunks.Where(c => c.Score > 0 && c.Chunk.ToLower().Contains(entity.ToLower())).ToList();
                    if (matchingChunks.Any())
                    {
                        _logger.LogInformation("Found {Count} chunks containing entity '{Entity}'", matchingChunks.Count, entity);
                    }
                }
                
                // Sort chunks by score and take top ones
                relevantChunks = scoredChunks
                    .OrderByDescending(x => x.Score)
                    .Take(Math.Max(minRelevantChunks, scoredChunks.Count(c => c.Score > 0)))
                    .Select(x => x.Chunk)
                    .ToList();
                
                // If no relevant chunks found, fall back to first and spaced chunks
                if (relevantChunks.Count == 0)
                {
                    _logger.LogWarning("No relevant chunks found, falling back to distributed chunk sampling");
                    
                    // For large documents, take samples from beginning, middle, and end
                    if (isLargeDocument)
                    {
                        var chunks = new List<string>();
                        // Take from beginning
                        chunks.AddRange(documentInfo.Chunks.Take(3));
                        
                        // Take from middle
                        int middleIndex = documentInfo.Chunks.Count / 2;
                        chunks.AddRange(documentInfo.Chunks
                            .Skip(middleIndex - 1)
                            .Take(3));
                            
                        // Take from end if document is very large
                        if (documentInfo.Chunks.Count > 200)
                        {
                            chunks.AddRange(documentInfo.Chunks
                                .Skip(documentInfo.Chunks.Count - 3)
                                .Take(3));
                        }
                        
                        relevantChunks = chunks;
                    }
                    else
                    {
                        // For smaller documents, just take the first few chunks
                        int maxChunksToInclude = Math.Min(5, documentInfo.Chunks.Count);
                        relevantChunks = documentInfo.Chunks.Take(maxChunksToInclude).ToList();
                    }
                }
            }
            
            return relevantChunks;
        }
        
        /// <summary>
        /// Extracts meaningful keywords from a query
        /// </summary>
        private List<string> ExtractKeywords(string query)
        {
            // Common stopwords to exclude
            var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a", "an", "the", "in", "on", "at", "of", "for", "to", "by", "with", 
                "and", "or", "but", "is", "are", "was", "were", "be", "been", "being",
                "have", "has", "had", "do", "does", "did", "will", "would", "shall", "should",
                "can", "could", "may", "might", "must", "about", "there", "their", "what", 
                "when", "where", "who", "whom", "which", "why", "how"
            };
            
            // Normalize query
            query = Regex.Replace(query.ToLower(), @"[^\w\s]", " ");
            
            // Extract single words
            var words = query.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopwords.Contains(w))
                .ToList();
                
            // Also look for 2-3 word phrases for more context
            var phrases = new List<string>();
            var queryWords = query.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < queryWords.Length - 1; i++)
            {
                // 2-word phrases
                var phrase2 = queryWords[i] + " " + queryWords[i + 1];
                if (phrase2.Length > 5) phrases.Add(phrase2);
                
                // 3-word phrases
                if (i < queryWords.Length - 2)
                {
                    var phrase3 = queryWords[i] + " " + queryWords[i + 1] + " " + queryWords[i + 2];
                    if (phrase3.Length > 8) phrases.Add(phrase3);
                }
            }
            
            // Combine single words and phrases
            var keywords = new List<string>();
            keywords.AddRange(words);
            keywords.AddRange(phrases);
            
            return keywords;
        }
    }
}
