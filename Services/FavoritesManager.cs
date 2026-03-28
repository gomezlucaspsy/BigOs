using System.IO;
using System.Text.Json;
using UnixBrowser.Models;

namespace UnixBrowser.Services
{
    public class FavoritesManager
    {
        private readonly string _filePath;
        private List<Favorite> _favorites;

        public IReadOnlyList<Favorite> Favorites => _favorites.AsReadOnly();
        public event Action? OnChanged;

        public FavoritesManager()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UnixBrowser");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "favorites.json");
            _favorites = Load();
        }

        public void Add(string title, string url)
        {
            if (_favorites.Any(f => f.Url == url)) return;
            _favorites.Add(new Favorite { Title = title, Url = url, AddedAt = DateTime.Now });
            Save();
            OnChanged?.Invoke();
        }

        public void Remove(string url)
        {
            _favorites.RemoveAll(f => f.Url == url);
            Save();
            OnChanged?.Invoke();
        }

        public bool IsFavorite(string url) => _favorites.Any(f => f.Url == url);

        private List<Favorite> Load()
        {
            try
            {
                if (File.Exists(_filePath))
                    return JsonSerializer.Deserialize<List<Favorite>>(
                        File.ReadAllText(_filePath)) ?? [];
            }
            catch { }
            return [];
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(_filePath,
                    JsonSerializer.Serialize(_favorites,
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
