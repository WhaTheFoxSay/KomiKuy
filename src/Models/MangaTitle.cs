using System;
using System.Collections.Generic;

namespace Mangaplus.Models
{
    public class MangaTitle
    {
        public int TitleId { get; set; }
        public string MangaUrl { get; set; } = "";
        public string Name { get; set; } = "";
        public string AlternativeName { get; set; } = "";
        public string Author { get; set; } = "";
        public string PortraitImageUrl { get; set; } = "";
        public string LandscapeImageUrl { get; set; } = "";
        public string ViewCount { get; set; } = "";
        public string ComicType { get; set; } = "Manga"; // Manga, Manhwa, Manhua
        public string Theme { get; set; } = "";
        public string Status { get; set; } = "Ongoing";
        public string Rating { get; set; } = "15+";
        public string ReadingDirection { get; set; } = "Kanan ke kiri";
        public List<string> Genres { get; set; } = new List<string>();
        public bool IsOngoing => Status.Equals("Ongoing", StringComparison.OrdinalIgnoreCase);
        public string LatestChapterName { get; set; } = "";
        public string LatestChapterUrl { get; set; } = "";
        public string FirstChapterName { get; set; } = "";
        public string FirstChapterUrl { get; set; } = "";
        public string Synopsis { get; set; } = "";
        public int Ranking { get; set; } = 0;
        public int LanguageCode { get; set; } = 3; // 3 = Bahasa Indonesia
        public string LanguageName { get; set; } = "Bahasa Indonesia";
        public string UpdatedTimeAgo { get; set; } = "";
        public string LastUpdateDateTime { get; set; } = "";
        public string DisplayUpdateInfo
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(LastUpdateDateTime))
                    return LastUpdateDateTime;
                if (!string.IsNullOrWhiteSpace(UpdatedTimeAgo))
                    return UpdatedTimeAgo;
                return "Baru saja diperbarui";
            }
        }

        public string DisplayRanking => Ranking > 0 ? $"#{Ranking}" : "";
        public string DisplayStatus => Status;
        public string DisplayType => ComicType;
        public string DisplayAuthor => string.IsNullOrWhiteSpace(Author) ? "Tite Kubo" : Author;

        public static string ResolveLanguage(int code)
        {
            switch (code)
            {
                case 0: return "English";
                case 1: return "Español";
                case 2: return "Français";
                case 3: return "Bahasa Indonesia";
                case 4: return "Português";
                case 5: return "Русский";
                case 6: return "ภาษาไทย";
                case 7: return "Deutsch";
                default: return "Bahasa Indonesia";
            }
        }
    }

    public class TitleDetailNavigationArgs
    {
        public MangaTitle Title { get; set; }
        public ReadingHistoryItem ResumeHistory { get; set; }
    }

    public class MangaDetailResult
    {
        public MangaTitle Detail { get; set; }
        public List<ChapterItem> Chapters { get; set; } = new List<ChapterItem>();
    }

    public class FeaturedBanner
    {
        public int TitleId { get; set; }
        public string MangaUrl { get; set; } = "";
        public string TitleName { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Subtitle { get; set; } = "";
    }

    public class GenreItem
    {
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Description { get; set; } = "";
        public string BadgeColor { get; set; } = "#2563EB";
    }
}
