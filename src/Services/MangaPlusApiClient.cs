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
    public class TitleDetailResult
    {
        public MangaTitle Title { get; set; }
        public string Synopsis { get; set; } = "";
        public string TotalViews { get; set; } = "";
        public List<ChapterItem> Chapters { get; set; } = new List<ChapterItem>();
        public List<FeaturedBanner> Banners { get; set; } = new List<FeaturedBanner>();
    }

    public class MangaPlusApiClient
    {
        private static MangaPlusApiClient _instance;
        public static MangaPlusApiClient Instance => _instance ?? (_instance = new MangaPlusApiClient());

        private const string BaseApiUrl = "https://jumpg-webapi.tokyo-cdn.com/api";
        private readonly HttpClient _httpClient;
        private readonly string _sessionToken;

        public MangaPlusApiClient()
        {
            _sessionToken = Guid.NewGuid().ToString();
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Origin", "https://mangaplus.shueisha.co.jp");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://mangaplus.shueisha.co.jp/");
            _httpClient.DefaultRequestHeaders.Add("SESSION-TOKEN", _sessionToken);
        }

        public async Task<List<MangaTitle>> GetAllTitlesAsync(bool forceRefresh = false)
        {
            var list = new List<MangaTitle>();

            if (!forceRefresh)
            {
                byte[] cached = await StorageService.Instance.ReadCacheAsync("all_titles.bin");
                if (cached != null && cached.Length > 0)
                {
                    list = ParseAllTitles(cached);
                    if (list.Count > 0) return list;
                }
            }

            try
            {
                byte[] raw = await _httpClient.GetByteArrayAsync($"{BaseApiUrl}/title_list/allV2");
                list = ParseAllTitles(raw);

                if (list.Count > 0)
                {
                    await StorageService.Instance.SaveCacheAsync("all_titles.bin", raw);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllTitlesAsync Error: {ex.Message}");
            }

            return list;
        }

        private List<MangaTitle> ParseAllTitles(byte[] raw)
        {
            var list = new List<MangaTitle>();
            if (raw == null || raw.Length == 0) return list;

            try
            {
                var rootFields = ProtobufReader.ParseFields(raw);
                var successField = rootFields.FirstOrDefault(f => f.Tag == 1);
                if (successField == null) return list;

                var successMsg = successField.AsMessage();
                var allTitlesField = successMsg.FirstOrDefault(f => f.Tag == 25 || f.Tag == 5);
                if (allTitlesField != null)
                {
                    var allTitlesMsg = allTitlesField.AsMessage();

                    foreach (var groupField in allTitlesMsg.Where(f => f.Tag == 1))
                    {
                        var groupMsg = groupField.AsMessage();
                        foreach (var titleField in groupMsg.Where(f => f.Tag == 2))
                        {
                            var t = ParseTitle(titleField.AsMessage());
                            if (t != null && t.TitleId > 0 && !string.IsNullOrWhiteSpace(t.Name))
                            {
                                list.Add(t);
                            }
                        }
                    }

                    if (list.Count == 0)
                    {
                        foreach (var titleField in allTitlesMsg.Where(f => f.Tag == 1))
                        {
                            var t = ParseTitle(titleField.AsMessage());
                            if (t != null && t.TitleId > 0 && !string.IsNullOrWhiteSpace(t.Name))
                            {
                                list.Add(t);
                            }
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        public async Task<List<MangaTitle>> GetRankedTitlesAsync(string lang = "eng", bool forceRefresh = false)
        {
            var list = new List<MangaTitle>();
            string cacheName = $"ranking_{lang}.bin";

            if (!forceRefresh)
            {
                byte[] cached = await StorageService.Instance.ReadCacheAsync(cacheName);
                if (cached != null && cached.Length > 0)
                {
                    list = ParseRankedTitles(cached, lang);
                    if (list.Count > 0) return list;
                }
            }

            try
            {
                byte[] raw = await _httpClient.GetByteArrayAsync($"{BaseApiUrl}/title_list/rankingV2?lang={lang}&type=hottest&clang={lang}");
                list = ParseRankedTitles(raw, lang);

                if (list.Count > 0)
                {
                    await StorageService.Instance.SaveCacheAsync(cacheName, raw);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetRankedTitlesAsync Error: {ex.Message}");
            }

            return list;
        }

        private List<MangaTitle> ParseRankedTitles(byte[] raw, string lang)
        {
            var list = new List<MangaTitle>();
            if (raw == null || raw.Length == 0) return list;

            try
            {
                var rootFields = ProtobufReader.ParseFields(raw);
                var successField = rootFields.FirstOrDefault(f => f.Tag == 1);
                if (successField == null) return list;

                var successMsg = successField.AsMessage();
                var rankingField = successMsg.FirstOrDefault(f => f.Tag == 37 || f.Tag == 6);
                if (rankingField != null)
                {
                    int rank = 1;
                    var rankingMsg = rankingField.AsMessage();

                    foreach (var rankedItemField in rankingMsg.Where(f => f.Tag == 3 || f.Tag == 1))
                    {
                        var rankedMsg = rankedItemField.AsMessage();
                        MangaTitle selectedTitle = null;

                        foreach (var titleField in rankedMsg.Where(f => f.Tag == 2 || f.Tag == 1))
                        {
                            var t = ParseTitle(titleField.AsMessage());
                            if (t != null && t.TitleId > 0 && !string.IsNullOrWhiteSpace(t.Name))
                            {
                                if (selectedTitle == null) selectedTitle = t;
                                if (t.LanguageName.ToLowerInvariant().Contains(lang) || (lang == "ind" && t.LanguageCode == 3))
                                {
                                    selectedTitle = t;
                                    break;
                                }
                            }
                        }

                        if (selectedTitle != null)
                        {
                            selectedTitle.Ranking = rank++;
                            list.Add(selectedTitle);
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        public async Task<TitleDetailResult> GetTitleDetailAsync(int titleId)
        {
            var result = new TitleDetailResult();
            try
            {
                byte[] raw = await _httpClient.GetByteArrayAsync($"{BaseApiUrl}/title_detailV3?title_id={titleId}");
                var rootFields = ProtobufReader.ParseFields(raw);
                var successField = rootFields.FirstOrDefault(f => f.Tag == 1);
                if (successField == null) return result;

                var successMsg = successField.AsMessage();
                var detailField = successMsg.FirstOrDefault(f => f.Tag == 8);
                if (detailField == null) return result;

                var detailMsg = detailField.AsMessage();

                // Tag 1: Title
                var titleField = detailMsg.FirstOrDefault(f => f.Tag == 1);
                if (titleField != null)
                {
                    result.Title = ParseTitle(titleField.AsMessage());
                }

                // Tag 3: Synopsis
                var synField = detailMsg.FirstOrDefault(f => f.Tag == 3);
                if (synField != null)
                {
                    result.Synopsis = synField.AsString();
                    if (result.Title != null) result.Title.Synopsis = result.Synopsis;
                }

                // Tag 18: Total Views
                var viewsField = detailMsg.FirstOrDefault(f => f.Tag == 18);
                if (viewsField != null)
                {
                    long totalViews = viewsField.AsInt64();
                    if (totalViews >= 1_000_000)
                        result.TotalViews = $"{(totalViews / 1_000_000.0):0.#}M View";
                    else if (totalViews >= 1_000)
                        result.TotalViews = $"{(totalViews / 1_000.0):0.#}K View";
                    else if (totalViews > 0)
                        result.TotalViews = $"{totalViews} View";
                }

                var chaptersDict = new Dictionary<int, ChapterItem>();
                int fallbackIdx = 1;

                // Chapter Groups: Tag 28, Tag 9, Tag 10
                foreach (var cgField in detailMsg.Where(f => f.Tag == 28 || f.Tag == 9 || f.Tag == 10))
                {
                    var cgMsg = cgField.AsMessage();
                    foreach (var cField in cgMsg.Where(f => f.Tag == 2 || f.Tag == 3 || f.Tag == 4 || f.Tag == 1))
                    {
                        var ch = ParseChapter(cField.AsMessage(), fallbackIdx++, cField.Tag);
                        if (ch != null && ch.ChapterId > 0 && !chaptersDict.ContainsKey(ch.ChapterId))
                        {
                            ch.TitleId = titleId;
                            if (result.Title != null) ch.TitleName = result.Title.Name;
                            chaptersDict[ch.ChapterId] = ch;
                        }
                    }
                }

                result.Chapters = chaptersDict.Values.OrderBy(c => c.ChapterNumber).ToList();

                // Banners: Tag 20 or Tag 17
                foreach (var bField in detailMsg.Where(f => f.Tag == 20 || f.Tag == 17))
                {
                    var bMsg = bField.AsMessage();
                    var banner = new FeaturedBanner
                    {
                        TitleId = titleId,
                        ImageUrl = bMsg.FirstOrDefault(f => f.Tag == 1)?.AsString() ?? "",
                        Subtitle = bMsg.FirstOrDefault(f => f.Tag == 2)?.AsString() ?? ""
                    };
                    if (!string.IsNullOrEmpty(banner.ImageUrl))
                        result.Banners.Add(banner);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTitleDetailAsync Error: {ex.Message}");
            }
            return result;
        }

        public async Task<List<MangaPageItem>> GetChapterPagesAsync(int chapterId)
        {
            var list = new List<MangaPageItem>();
            try
            {
                byte[] raw = await _httpClient.GetByteArrayAsync($"{BaseApiUrl}/manga_viewer?chapter_id={chapterId}&split=yes&img_quality=high");
                var rootFields = ProtobufReader.ParseFields(raw);

                // Check for server error / locked response
                var errorField = rootFields.FirstOrDefault(f => f.Tag == 2);
                if (errorField != null)
                {
                    System.Diagnostics.Debug.WriteLine($"MangaViewer Error (Locked/Ticketed chapter): {chapterId}");
                    return list;
                }

                var successField = rootFields.FirstOrDefault(f => f.Tag == 1);
                if (successField == null) return list;

                var successMsg = successField.AsMessage();
                var viewerField = successMsg.FirstOrDefault(f => f.Tag == 10);
                if (viewerField == null) return list;

                var viewerMsg = viewerField.AsMessage();
                int pageIndex = 1;

                foreach (var pageField in viewerMsg.Where(f => f.Tag == 1))
                {
                    var pageMsg = pageField.AsMessage();
                    var mangaPageField = pageMsg.FirstOrDefault(f => f.Tag == 1);
                    if (mangaPageField != null)
                    {
                        var mpMsg = mangaPageField.AsMessage();
                        string url = mpMsg.FirstOrDefault(f => f.Tag == 1)?.AsString() ?? "";
                        string key = mpMsg.FirstOrDefault(f => f.Tag == 5)?.AsString() ?? "";

                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            list.Add(new MangaPageItem
                            {
                                PageNumber = pageIndex++,
                                ImageUrl = url,
                                EncryptionKeyHex = key
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetChapterPagesAsync Error: {ex.Message}");
            }
            return list;
        }

        private MangaTitle ParseTitle(List<ProtoField> fields)
        {
            if (fields == null || fields.Count == 0) return null;

            var t = new MangaTitle();
            foreach (var f in fields)
            {
                switch (f.Tag)
                {
                    case 1: t.TitleId = f.AsInt32(); break;
                    case 2: t.Name = f.AsString(); break;
                    case 3: t.Author = f.AsString(); break;
                    case 4: t.PortraitImageUrl = f.AsString(); break;
                    case 5: t.LandscapeImageUrl = f.AsString(); break;
                    case 6: t.ViewCount = f.AsInt32().ToString("N0"); break;
                    case 7:
                        t.LanguageCode = f.AsInt32();
                        t.LanguageName = MangaTitle.ResolveLanguage(t.LanguageCode);
                        break;
                }
            }
            return t;
        }

        private ChapterItem ParseChapter(List<ProtoField> fields, int indexFallback, int cgTag)
        {
            if (fields == null || fields.Count == 0) return null;

            var c = new ChapterItem();
            foreach (var f in fields)
            {
                switch (f.Tag)
                {
                    case 1: c.TitleId = f.AsInt32(); break;
                    case 2: c.ChapterId = f.AsInt32(); break;
                    case 3: c.Name = f.AsString(); break;
                    case 4: c.SubTitle = f.AsString(); break;
                    case 5: c.ThumbnailUrl = f.AsString(); break;
                    case 6:
                        c.StartTimestamp = f.AsInt64();
                        if (c.StartTimestamp > 0)
                        {
                            try
                            {
                                var dt = DateTimeOffset.FromUnixTimeSeconds(c.StartTimestamp).LocalDateTime;
                                c.ReleaseDateString = dt.ToString("MMM dd, yyyy");
                            }
                            catch { }
                        }
                        break;
                    case 7: c.EndTimestamp = f.AsInt64(); break;
                    case 13: c.ViewCount = f.AsInt64(); break;
                    case 14: c.CommentCount = f.AsInt32(); break;
                }
            }

            if (c.ChapterId == 0 && c.TitleId > 0 && !string.IsNullOrEmpty(c.Name))
            {
                c.ChapterId = c.TitleId;
            }

            c.ChapterNumber = ChapterItem.ExtractChapterNumber(c.Name, c.SubTitle, indexFallback);
            
            // In Shueisha MangaPlus Protobuf, CG Tag 3 represents middle/ticket-locked chapters
            c.IsLocked = (cgTag == 3);
            c.IsFree = !c.IsLocked;

            return c;
        }
    }
}
