using System;

namespace Mangaplus.Models
{
    public class ReadingHistoryItem
    {
        public int TitleId { get; set; }
        public string TitleName { get; set; } = "";
        public string CoverUrl { get; set; } = "";
        public string MangaUrl { get; set; } = "";
        public int ChapterId { get; set; }
        public string ChapterName { get; set; } = "";
        public string ChapterUrl { get; set; } = "";
        public int LastReadPageIndex { get; set; } = 0;
        public int TotalPages { get; set; } = 0;
        public DateTime LastReadTime { get; set; } = DateTime.UtcNow;

        public string TimeAgoString
        {
            get
            {
                var diff = DateTime.UtcNow - LastReadTime;
                if (diff.TotalMinutes < 1) return "Baru saja";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} menit lalu";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} jam lalu";
                return $"{(int)diff.TotalDays} hari lalu";
            }
        }
    }
}
