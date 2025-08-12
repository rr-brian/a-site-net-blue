using System;
using System.Collections.Generic;

namespace Backend.Models
{
    public class DocumentInfo
    {
        public string FileName { get; set; } = "";
        public List<string> Chunks { get; set; } = new List<string>();
        public List<ChunkMetadata> ChunkMetadata { get; set; } = new List<ChunkMetadata>();
        public int TotalLength { get; set; }
        public Dictionary<string, List<int>> EntityIndex { get; set; } = new Dictionary<string, List<int>>();
        public string Summary { get; set; } = "";
        public DateTime UploadTime { get; set; }
        
        // For backward compatibility and simple access
        public string GetFullContent()
        {
            return string.Join("\n\n", Chunks);
        }
    }
}
