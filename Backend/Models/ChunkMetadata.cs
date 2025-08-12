namespace Backend.Models
{
    public class ChunkMetadata
    {
        public int ChunkIndex { get; set; }
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
        public List<int> Pages { get; set; } = new List<int>();
        public List<string> KeyEntities { get; set; } = new List<string>();
        public float RelevanceScore { get; set; } = 0;
    }
}
