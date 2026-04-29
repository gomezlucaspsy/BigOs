// ============================================================
// Tab.cs — Browser Tab Model
// Purpose : Lightweight data record representing one browser tab.
//           Holds no logic — TabManager owns all tab behavior.
// ============================================================
namespace UnixBrowser.Models
{
    /// <summary>
    /// A single browser tab. Stores display state only.
    /// The WebView2 rendering engine is shared — not per-tab.
    /// </summary>
    public class Tab
    {
        /// <summary>Unique identifier. Used as the key in TabManager.</summary>
        public string   Id        { get; set; } = Guid.NewGuid().ToString();

        /// <summary>The page title shown on the tab strip.</summary>
        public string   Title     { get; set; } = "New Tab";

        /// <summary>The last known URL for this tab.</summary>
        public string   Url       { get; set; } = "about:blank";

        /// <summary>When the tab was opened. Used for session history.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
