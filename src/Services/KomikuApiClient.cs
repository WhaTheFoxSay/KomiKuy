using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using Mangaplus.Models;

namespace Mangaplus.Services
{
    public class KomikuCategoriesResult
    {
        public List<MangaTitle> Populer { get; set; } = new List<MangaTitle>();
        public List<MangaTitle> Terbaru { get; set; } = new List<MangaTitle>();
        public List<MangaTitle> Manga { get; set; } = new List<MangaTitle>();
        public List<MangaTitle> Manhwa { get; set; } = new List<MangaTitle>();
        public List<MangaTitle> Manhua { get; set; } = new List<MangaTitle>();
        public List<MangaTitle> All { get; set; } = new List<MangaTitle>();
    }

    public class KomikuApiClient
    {
        private static KomikuApiClient _instance;
        public static KomikuApiClient Instance => _instance ?? (_instance = new KomikuApiClient());

        private readonly HttpClient _client;
        private const string BaseUrl = "https://komiku.org";
        private const string ApiBaseUrl = "https://api.komiku.org";
        private StorageFolder _cacheFolder;
        private KomikuCategoriesResult _cachedCatalogInMemory;
        private readonly Dictionary<string, MangaDetailResult> _detailMemoryCache = new Dictionary<string, MangaDetailResult>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<MangaPageItem>> _chapterPagesMemoryCache = new Dictionary<string, List<MangaPageItem>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _textMemoryCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public KomikuApiClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                MaxConnectionsPerServer = 24
            };
            _client = new HttpClient(handler);
            _client.Timeout = TimeSpan.FromSeconds(8);
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        }

        private async Task<StorageFolder> GetCacheFolderAsync()
        {
            if (_cacheFolder == null)
            {
                try
                {
                    _cacheFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("KomikuCache", CreationCollisionOption.OpenIfExists);
                }
                catch { }
            }
            return _cacheFolder;
        }

        private async Task SaveTextCacheAsync(string key, string text)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(text)) return;
            lock (_textMemoryCache)
            {
                _textMemoryCache[key] = text;
            }
            try
            {
                var folder = await GetCacheFolderAsync();
                if (folder != null)
                {
                    var file = await folder.CreateFileAsync(key, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteTextAsync(file, text);
                }
            }
            catch { }
        }

        private async Task<string> ReadTextCacheAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            lock (_textMemoryCache)
            {
                if (_textMemoryCache.TryGetValue(key, out var memVal))
                    return memVal;
            }
            try
            {
                var folder = await GetCacheFolderAsync();
                if (folder != null)
                {
                    var file = await folder.TryGetItemAsync(key) as StorageFile;
                    if (file != null)
                    {
                        string text = await FileIO.ReadTextAsync(file);
                        lock (_textMemoryCache)
                        {
                            _textMemoryCache[key] = text;
                        }
                        return text;
                    }
                }
            }
            catch { }
            return null;
        }

        public async Task<KomikuCategoriesResult> GetHomeCatalogAsync(bool forceRefresh = false)
        {
            // 1. Instant in-memory cache (0.0 ms)
            if (!forceRefresh && _cachedCatalogInMemory != null && _cachedCatalogInMemory.All.Count > 0)
            {
                _ = RefreshHomeCatalogFromLiveApiAsync();
                return _cachedCatalogInMemory;
            }

            string cacheKey = "home_catalog.html";
            if (!forceRefresh)
            {
                // 2. Check local disk cache (1.0 ms)
                string cached = await ReadTextCacheAsync(cacheKey);
                if (!string.IsNullOrEmpty(cached))
                {
                    var cachedRes = ParseHomeCatalog(cached);
                    if (cachedRes.All.Count > 0)
                    {
                        _cachedCatalogInMemory = cachedRes;
                        _ = RefreshHomeCatalogFromLiveApiAsync();
                        PrefetchTopMangaDetails(cachedRes.Populer.Take(10));
                        return cachedRes;
                    }
                }

                // 3. Load bundled asset catalog (0 ms instant startup)
                try
                {
                    var assetFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/KomikuCatalog.html"));
                    if (assetFile != null)
                    {
                        string bundledHtml = await FileIO.ReadTextAsync(assetFile);
                        if (!string.IsNullOrEmpty(bundledHtml))
                        {
                            var bundledRes = ParseHomeCatalog(bundledHtml);
                            _cachedCatalogInMemory = bundledRes;
                            _ = RefreshHomeCatalogFromLiveApiAsync();
                            return bundledRes;
                        }
                    }
                }
                catch { }
            }

            // 4. Direct Live API Fetch
            try
            {
                string html = await _client.GetStringAsync($"{BaseUrl}/");
                await SaveTextCacheAsync(cacheKey, html);
                var res = ParseHomeCatalog(html);
                _cachedCatalogInMemory = res;
                PrefetchTopMangaDetails(res.Populer.Take(10));
                return res;
            }
            catch
            {
                string cached = await ReadTextCacheAsync(cacheKey);
                if (!string.IsNullOrEmpty(cached))
                    return ParseHomeCatalog(cached);
                return new KomikuCategoriesResult();
            }
        }

        public async Task<KomikuCategoriesResult> RefreshHomeCatalogFromLiveApiAsync()
        {
            try
            {
                string html = await _client.GetStringAsync($"{BaseUrl}/");
                await SaveTextCacheAsync("home_catalog.html", html);
                var res = ParseHomeCatalog(html);
                if (res.All.Count > 0)
                {
                    _cachedCatalogInMemory = res;
                }
                return res;
            }
            catch
            {
                return _cachedCatalogInMemory ?? new KomikuCategoriesResult();
            }
        }

        private void PrefetchTopMangaDetails(IEnumerable<MangaTitle> titles)
        {
            if (titles == null) return;
            Task.Run(async () =>
            {
                foreach (var t in titles)
                {
                    if (!string.IsNullOrEmpty(t.MangaUrl))
                    {
                        try
                        {
                            string safeKey = "detail_" + Math.Abs(t.MangaUrl.GetHashCode()) + ".html";
                            string cached = await ReadTextCacheAsync(safeKey);
                            if (string.IsNullOrEmpty(cached))
                            {
                                string fullUrl = t.MangaUrl.StartsWith("http") ? t.MangaUrl : $"{BaseUrl}{t.MangaUrl}";
                                string html = await _client.GetStringAsync(fullUrl);
                                await SaveTextCacheAsync(safeKey, html);
                            }
                        }
                        catch { }
                    }
                }
            });
        }

        public async Task<List<MangaTitle>> SearchMangaAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<MangaTitle>();

            string q = query.Trim();
            try
            {
                string url = $"{ApiBaseUrl}/?post_type=manga&s={Uri.EscapeDataString(q)}";
                string html = await _client.GetStringAsync(url);
                var list = FastHtmlParser.ParseCards(html);
                if (list != null && list.Count > 0) return list;
            }
            catch { }

            try
            {
                string url = $"{BaseUrl}/?post_type=manga&s={Uri.EscapeDataString(q)}";
                string html = await _client.GetStringAsync(url);
                return FastHtmlParser.ParseCards(html);
            }
            catch
            {
                return new List<MangaTitle>();
            }
        }

        public async Task<List<MangaTitle>> GetComicsByGenreAsync(string genreSlug)
        {
            if (string.IsNullOrWhiteSpace(genreSlug))
                return new List<MangaTitle>();

            string slug = genreSlug.Trim().ToLowerInvariant();
            string safeKey = "genre_" + slug + ".html";

            string cached = await ReadTextCacheAsync(safeKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedList = FastHtmlParser.ParseCards(cached);
                if (cachedList != null && cachedList.Count > 0)
                {
                    _ = RefreshGenreCacheAsync(slug, safeKey);
                    return cachedList;
                }
            }

            try
            {
                string url = $"{ApiBaseUrl}/genre/{slug}/";
                string html = await _client.GetStringAsync(url);
                await SaveTextCacheAsync(safeKey, html);
                var list = FastHtmlParser.ParseCards(html);
                if (list != null && list.Count > 0) return list;
            }
            catch { }

            try
            {
                string url = $"{BaseUrl}/genre/{slug}/";
                string html = await _client.GetStringAsync(url);
                await SaveTextCacheAsync(safeKey, html);
                return FastHtmlParser.ParseCards(html);
            }
            catch
            {
                return new List<MangaTitle>();
            }
        }

        private async Task RefreshGenreCacheAsync(string slug, string safeKey)
        {
            try
            {
                string url = $"{ApiBaseUrl}/genre/{slug}/";
                string html = await _client.GetStringAsync(url);
                await SaveTextCacheAsync(safeKey, html);
            }
            catch { }
        }

        public async Task<MangaDetailResult> GetMangaDetailAsync(string mangaUrl)
        {
            if (string.IsNullOrWhiteSpace(mangaUrl))
                return new MangaDetailResult();

            lock (_detailMemoryCache)
            {
                if (_detailMemoryCache.TryGetValue(mangaUrl, out var memRes) && memRes?.Chapters?.Count > 0)
                {
                    return memRes;
                }
            }

            var result = new MangaDetailResult();
            var title = new MangaTitle
            {
                MangaUrl = mangaUrl,
                TitleId = Math.Abs((mangaUrl ?? "").GetHashCode())
            };
            var chapters = new List<ChapterItem>();
            result.Detail = title;
            result.Chapters = chapters;

            string safeKey = "detail_" + Math.Abs(mangaUrl.GetHashCode()) + ".html";
            string cached = await ReadTextCacheAsync(safeKey);
            if (!string.IsNullOrEmpty(cached))
            {
                FastHtmlParser.ParseMangaDetail(cached, title, chapters, BaseUrl);
                if (chapters.Count > 0)
                {
                    lock (_detailMemoryCache)
                    {
                        _detailMemoryCache[mangaUrl] = result;
                    }
                    _ = RefreshDetailCacheAsync(mangaUrl, safeKey);
                    return result;
                }
            }

            try
            {
                string fullUrl = mangaUrl.StartsWith("http") ? mangaUrl : $"{BaseUrl}{mangaUrl}";
                string html = await _client.GetStringAsync(fullUrl);
                await SaveTextCacheAsync(safeKey, html);
                FastHtmlParser.ParseMangaDetail(html, title, chapters, BaseUrl);

                if (chapters.Count > 0)
                {
                    lock (_detailMemoryCache)
                    {
                        _detailMemoryCache[mangaUrl] = result;
                    }
                }
            }
            catch { }

            return result;
        }

        private async Task RefreshDetailCacheAsync(string mangaUrl, string safeKey)
        {
            try
            {
                string fullUrl = mangaUrl.StartsWith("http") ? mangaUrl : $"{BaseUrl}{mangaUrl}";
                string html = await _client.GetStringAsync(fullUrl);
                await SaveTextCacheAsync(safeKey, html);
            }
            catch { }
        }

        public async Task<List<MangaPageItem>> GetChapterPagesAsync(string chapterUrl)
        {
            if (string.IsNullOrWhiteSpace(chapterUrl))
                return new List<MangaPageItem>();

            lock (_chapterPagesMemoryCache)
            {
                if (_chapterPagesMemoryCache.TryGetValue(chapterUrl, out var memPages) && memPages?.Count > 0)
                {
                    return memPages;
                }
            }

            var pages = new List<MangaPageItem>();
            string safeKey = "pages_" + Math.Abs(chapterUrl.GetHashCode()) + ".txt";
            string cached = await ReadTextCacheAsync(safeKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var lines = cached.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    pages.Add(new MangaPageItem
                    {
                        PageNumber = i + 1,
                        ImageUrl = lines[i].Trim(),
                        EncryptionKeyHex = "",
                        IsSpread = false,
                        IsLastPage = (i == lines.Length - 1)
                    });
                }

                if (pages.Count > 0)
                {
                    lock (_chapterPagesMemoryCache)
                    {
                        _chapterPagesMemoryCache[chapterUrl] = pages;
                    }
                    return pages;
                }
            }

            try
            {
                string fullUrl = chapterUrl.StartsWith("http") ? chapterUrl : $"{BaseUrl}{chapterUrl}";
                string html = await _client.GetStringAsync(fullUrl);

                pages = FastHtmlParser.ParseChapterPages(html);

                if (pages.Count > 0)
                {
                    lock (_chapterPagesMemoryCache)
                    {
                        _chapterPagesMemoryCache[chapterUrl] = pages;
                    }
                    var rawUrls = pages.Select(p => p.ImageUrl).ToList();
                    await SaveTextCacheAsync(safeKey, string.Join("\n", rawUrls));
                }
            }
            catch { }

            return pages;
        }

        public void PrefetchChapterInBackground(string chapterUrl)
        {
            if (string.IsNullOrWhiteSpace(chapterUrl)) return;
            Task.Run(async () =>
            {
                try
                {
                    var pages = await GetChapterPagesAsync(chapterUrl);
                    if (pages != null && pages.Count > 0)
                    {
                        var urls = pages.Select(p => p.ImageUrl).ToList();
                        MangaImageDecryptor.Instance.PreloadImagesInBackground(urls);
                    }
                }
                catch { }
            });
        }

        private KomikuCategoriesResult ParseHomeCatalog(string html)
        {
            var result = new KomikuCategoriesResult();
            var all = FastHtmlParser.ParseCards(html);
            result.All = all;

            foreach (var item in all)
            {
                if (item.ComicType == "Manhwa")
                {
                    result.Manhwa.Add(item);
                }
                else if (item.ComicType == "Manhua")
                {
                    result.Manhua.Add(item);
                }
                else
                {
                    result.Manga.Add(item);
                }
            }

            result.Populer = all.Take(50).ToList();
            result.Terbaru = all.Skip(15).Take(50).ToList();

            return result;
        }
    }
}
