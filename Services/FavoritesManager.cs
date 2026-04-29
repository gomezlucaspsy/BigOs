// ============================================================
// FavoritesManager.cs — Persistent Favorites Store
// Purpose : Manages the user's saved pages. Persists to disk
//           as JSON so favorites survive app restarts.
//           Single responsibility: store, load, and notify.
// ============================================================
using System.IO;
using System.Text.Json;
using UnixBrowser.Models;

namespace UnixBrowser.Services
{
    /// <summary>
    /// Stores and retrieves the user's favorite URLs.
    /// Persists to %AppData%\UnixBrowser\favorites.json.
    /// Notifies subscribers via OnChanged when the list mutates.
    /// </summary>
    public class FavoritesManager
    {
        private readonly string      _filePath;   // Absolute path to the JSON file on disk
        private          List<Favorite> _favorites; // In-memory list; source of truth during session

        public IReadOnlyList<Favorite> Favorites => _favorites.AsReadOnly();

        /// <summary>Fired after any Add or Remove so the UI can re-render.</summary>
        public event Action? OnChanged;

        public FavoritesManager()
        {
            // Store data under the standard Windows user app data folder
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UnixBrowser");
            Directory.CreateDirectory(dir);
            _filePath  = Path.Combine(dir, "favorites.json");
            _favorites = Load();
        }

        /// <summary>
        /// Adds a URL to favorites. Silently ignores duplicates
        /// so the UI does not need to check first.
        /// </summary>
        public void Add(string title, string url)
        {
            if (_favorites.Any(f => f.Url == url)) return; // Already saved
            _favorites.Add(new Favorite { Title = title, Url = url, AddedAt = DateTime.Now });
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>Removes all entries matching the given URL.</summary>
        public void Remove(string url)
        {
            _favorites.RemoveAll(f => f.Url == url);
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>Returns true if the URL is already in the favorites list.</summary>
        public bool IsFavorite(string url) => _favorites.Any(f => f.Url == url);

        /// <summary>
        /// Deserializes favorites from disk. Returns an empty list on any
        /// failure — a missing or corrupt file is not a fatal error.
        /// </summary>
        private List<Favorite> Load()
        {
            try
            {
                if (File.Exists(_filePath))
                    return JsonSerializer.Deserialize<List<Favorite>>(
                        File.ReadAllText(_filePath)) ?? [];
            }
            catch { /* Corrupt file — start fresh */ }
            return [];
        }

        /// <summary>
        /// Serializes favorites to disk with indentation for human readability.
        /// Failures are silently swallowed — a save error should never crash the app.
        /// </summary>
        private void Save()
        {
            try
            {
                File.WriteAllText(_filePath,
                    JsonSerializer.Serialize(_favorites,
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* Disk write failed — in-memory state is still valid */ }
        }
    }
}
