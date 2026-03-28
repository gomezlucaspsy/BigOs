namespace UnixBrowser.Models
{
    public class Tab
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "New Tab";
        public string Url { get; set; } = "about:blank";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
