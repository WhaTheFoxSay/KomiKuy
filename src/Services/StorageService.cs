using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;
using Mangaplus.Models;

namespace Mangaplus.Services
{
    public class StorageService
    {
        private static StorageService _instance;
        public static StorageService Instance => _instance ?? (_instance = new StorageService());

        private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;
        private readonly HashSet<int> _favorites = new HashSet<int>();
        private readonly Dictionary<int, ReadingHistoryItem> _history = new Dictionary<int, ReadingHistoryItem>();
        private readonly Dictionary<int, ReadingBookmark> _bookmarks = new Dictionary<int, ReadingBookmark>();
        private StorageFolder _cacheFolder;

        public StorageService()
        {
            LoadFavorites();
            LoadHistory();
            LoadBookmarks();
        }

        public string PreferredLanguage
        {
            get
            {
                if (_settings.Values.TryGetValue("MangaPlus_DefaultLanguage", out object val) && val is string s)
                    return s;
                return "eng"; // Default English
            }
            set
            {
                _settings.Values["MangaPlus_DefaultLanguage"] = value;
            }
        }

        public bool HasSelectedLanguage => _settings.Values.ContainsKey("MangaPlus_DefaultLanguage");

        public bool IsFavorite(int titleId) => _favorites.Contains(titleId);

        public void ToggleFavorite(int titleId)
        {
            if (_favorites.Contains(titleId))
                _favorites.Remove(titleId);
            else
                _favorites.Add(titleId);
            SaveFavorites();
        }

        public List<int> GetFavoriteIds() => _favorites.ToList();

        public void RecordReading(int titleId, string titleName, string coverUrl, string mangaUrl, int chapterId, string chapterName, string chapterUrl, int pageIndex, int totalPages)
        {
            if (titleId == 0 && !string.IsNullOrEmpty(titleName))
                titleId = Math.Abs(titleName.GetHashCode());

            _history[titleId] = new ReadingHistoryItem
            {
                TitleId = titleId,
                TitleName = titleName,
                CoverUrl = coverUrl,
                MangaUrl = mangaUrl,
                ChapterId = chapterId,
                ChapterName = chapterName,
                ChapterUrl = chapterUrl,
                LastReadPageIndex = pageIndex,
                TotalPages = totalPages,
                LastReadTime = DateTime.UtcNow
            };
            SaveHistory();
        }

        public void RecordReading(int titleId, string titleName, string coverUrl, int chapterId, string chapterName, int pageIndex, int totalPages)
        {
            RecordReading(titleId, titleName, coverUrl, "", chapterId, chapterName, "", pageIndex, totalPages);
        }

        public List<ReadingHistoryItem> GetHistoryList()
        {
            return _history.Values.OrderByDescending(h => h.LastReadTime).ToList();
        }

        public ReadingHistoryItem GetLastRead(int titleId)
        {
            if (_history.TryGetValue(titleId, out var item))
                return item;
            return null;
        }

        // ==================== GARIS BACA / READING BOOKMARKS ====================
        // Rule: 1 komik = 1 garis baca. Menambahkan garis baca baru akan otomatis mengganti garis baca sebelumnya.
        public void SetReadingBookmark(ReadingBookmark bookmark)
        {
            if (bookmark == null) return;
            if (bookmark.TitleId == 0)
            {
                bookmark.TitleId = Math.Abs((bookmark.MangaUrl ?? bookmark.TitleName ?? bookmark.ChapterUrl ?? "manga").GetHashCode());
            }

            // Remove any existing bookmark matching TitleName or TitleId
            var existingKeys = _bookmarks.Where(kv => kv.Key == bookmark.TitleId || (kv.Value.TitleName != null && kv.Value.TitleName.Equals(bookmark.TitleName, StringComparison.OrdinalIgnoreCase))).Select(kv => kv.Key).ToList();
            foreach (var k in existingKeys)
            {
                _bookmarks.Remove(k);
            }

            _bookmarks[bookmark.TitleId] = bookmark;
            SaveBookmarks();
        }

        public ReadingBookmark GetReadingBookmark(int titleId, string titleName = "", string mangaUrl = "")
        {
            if (titleId != 0 && _bookmarks.TryGetValue(titleId, out var bm))
                return bm;

            if (!string.IsNullOrEmpty(titleName))
            {
                var found = _bookmarks.Values.FirstOrDefault(b => b.TitleName != null && b.TitleName.Equals(titleName, StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }

            if (!string.IsNullOrEmpty(mangaUrl))
            {
                var found = _bookmarks.Values.FirstOrDefault(b => b.MangaUrl != null && b.MangaUrl.Equals(mangaUrl, StringComparison.OrdinalIgnoreCase));
                if (found != null) return found;
            }

            return null;
        }

        public void RemoveReadingBookmark(int titleId)
        {
            if (_bookmarks.ContainsKey(titleId))
            {
                _bookmarks.Remove(titleId);
                SaveBookmarks();
            }
        }

        public List<ReadingBookmark> GetAllReadingBookmarks()
        {
            return _bookmarks.Values.OrderByDescending(b => b.MarkedAt).ToList();
        }

        private void LoadBookmarks()
        {
            try
            {
                if (_settings.Values.TryGetValue("MangaPlus_Bookmarks", out object val) && val is string str)
                {
                    var lines = str.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 8 && int.TryParse(parts[0], out int tid))
                        {
                            _bookmarks[tid] = new ReadingBookmark
                            {
                                TitleId = tid,
                                TitleName = parts[1],
                                CoverUrl = parts[2],
                                MangaUrl = parts[3],
                                ChapterUrl = parts[4],
                                ChapterName = parts[5],
                                ChapterId = int.Parse(parts[6]),
                                PageNumber = int.Parse(parts[7]),
                                MarkedAt = parts.Length >= 9 && long.TryParse(parts[8], out long ticks) ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.UtcNow
                            };
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveBookmarks()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var b in _bookmarks.Values)
                {
                    sb.Append($"{b.TitleId}|{b.TitleName}|{b.CoverUrl}|{b.MangaUrl}|{b.ChapterUrl}|{b.ChapterName}|{b.ChapterId}|{b.PageNumber}|{b.MarkedAt.Ticks}\n");
                }
                _settings.Values["MangaPlus_Bookmarks"] = sb.ToString();
            }
            catch { }
        }

        // ==================== DISK CACHE ====================
        public async Task SaveCacheAsync(string fileName, byte[] data)
        {
            try
            {
                if (_cacheFolder == null)
                    _cacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("ApiCache", CreationCollisionOption.OpenIfExists);

                var file = await _cacheFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteBytesAsync(file, data);
            }
            catch { }
        }

        public async Task<byte[]> ReadCacheAsync(string fileName)
        {
            try
            {
                if (_cacheFolder == null)
                    _cacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("ApiCache", CreationCollisionOption.OpenIfExists);

                var file = await _cacheFolder.TryGetItemAsync(fileName) as StorageFile;
                if (file != null)
                {
                    var buf = await FileIO.ReadBufferAsync(file);
                    return buf.ToArray();
                }
            }
            catch { }
            return null;
        }

        private void LoadFavorites()
        {
            try
            {
                if (_settings.Values.TryGetValue("MangaPlus_Favorites", out object val) && val is string str)
                {
                    var ids = str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var idStr in ids)
                    {
                        if (int.TryParse(idStr, out int id))
                            _favorites.Add(id);
                    }
                }
            }
            catch { }
        }

        private void SaveFavorites()
        {
            try
            {
                string joined = string.Join(",", _favorites);
                _settings.Values["MangaPlus_Favorites"] = joined;
            }
            catch { }
        }

        private void LoadHistory()
        {
            try
            {
                if (_settings.Values.TryGetValue("MangaPlus_History", out object val) && val is string str)
                {
                    var lines = str.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 8 && int.TryParse(parts[0], out int tid))
                        {
                            _history[tid] = new ReadingHistoryItem
                            {
                                TitleId = tid,
                                TitleName = parts[1],
                                CoverUrl = parts[2],
                                ChapterId = int.Parse(parts[3]),
                                ChapterName = parts[4],
                                LastReadPageIndex = int.Parse(parts[5]),
                                TotalPages = int.Parse(parts[6]),
                                LastReadTime = new DateTime(long.Parse(parts[7]), DateTimeKind.Utc)
                            };
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveHistory()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var h in _history.Values)
                {
                    sb.Append($"{h.TitleId}|{h.TitleName}|{h.CoverUrl}|{h.ChapterId}|{h.ChapterName}|{h.LastReadPageIndex}|{h.TotalPages}|{h.LastReadTime.Ticks}\n");
                }
                _settings.Values["MangaPlus_History"] = sb.ToString();
            }
            catch { }
        }
    }
}
