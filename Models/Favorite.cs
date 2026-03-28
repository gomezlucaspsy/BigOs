namespace UnixBrowser.Models
{
    public class Favorite
    {
        public string Title   { get; set; } = string.Empty;
        public string Url     { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}
