using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Windows.UI.Xaml.Media.Imaging;

namespace Mangaplus.Models
{
    public class ChapterItem
    {
        public int ChapterId { get; set; }
        public int TitleId { get; set; }
        public string TitleName { get; set; } = "";
        public string MangaUrl { get; set; } = "";
        public string ChapterUrl { get; set; } = "";
        public string Name { get; set; } = "";
        public string SubTitle { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public long StartTimestamp { get; set; }
        public long EndTimestamp { get; set; }
        public bool IsRead { get; set; } = false;
        public string ReleaseDateString { get; set; } = "";
        public long ViewCount { get; set; } = 0;
        public int CommentCount { get; set; } = 0;
        public double ChapterNumber { get; set; } = 0;
        public bool IsFree { get; set; } = true;
        public bool IsLocked { get; set; } = false;

        public string DisplayName => string.IsNullOrWhiteSpace(SubTitle) ? Name : SubTitle;

        public string DisplayNumber => string.IsNullOrWhiteSpace(Name) ? $"#{ChapterNumber:000}" : Name;

        public string DisplayShortName
        {
            get
            {
                if (ChapterNumber > 0)
                {
                    if (ChapterNumber == (int)ChapterNumber)
                        return $"Ch. {ChapterNumber:0}";
                    return $"Ch. {ChapterNumber:0.#}";
                }
                string n = (Name ?? "").Trim();
                if (n.Length > 8)
                    return n.Substring(0, 8);
                return string.IsNullOrEmpty(n) ? "Ch. ?" : n;
            }
        }

        public string DisplayViews
        {
            get
            {
                if (ViewCount >= 1_000_000) return $"👁 {(ViewCount / 1_000_000.0):0.#}M";
                if (ViewCount >= 1_000) return $"👁 {(ViewCount / 1_000.0):0.#}K";
                if (ViewCount > 0) return $"👁 {ViewCount}";
                return "👁 50K+";
            }
        }

        public string DisplayComments => CommentCount > 0 ? $"💬 {CommentCount}" : "";

        public static double ExtractChapterNumber(string name, string subtitle, int indexFallback)
        {
            try
            {
                var match = Regex.Match(name ?? "", @"\d+(\.\d+)?");
                if (match.Success && double.TryParse(match.Value, out double num))
                    return num;

                var matchSub = Regex.Match(subtitle ?? "", @"(?:Chapter|Ch\.|#)\s*(\d+(\.\d+)?)", RegexOptions.IgnoreCase);
                if (matchSub.Success && double.TryParse(matchSub.Groups[1].Value, out double numSub))
                    return numSub;
            }
            catch { }

            return indexFallback;
        }
    }

    public class ChapterRangeTag
    {
        public string Label { get; set; } = "";
        public double MinChapter { get; set; }
        public double MaxChapter { get; set; }
        public bool IsSelected { get; set; } = false;
    }

    public class ReaderContext
    {
        public ChapterItem CurrentChapter { get; set; }
        public List<ChapterItem> AllChapters { get; set; } = new List<ChapterItem>();
        public int CurrentIndex { get; set; } = 0;
        public int TargetPageNumber { get; set; } = 0; // Automatically scroll to bookmarked page if > 0

        public bool HasPrevious => AllChapters != null && CurrentIndex > 0;
        public bool HasNext => AllChapters != null && CurrentIndex < AllChapters.Count - 1;
    }

    public class ReadingBookmark
    {
        public int TitleId { get; set; }
        public string TitleName { get; set; } = "";
        public string CoverUrl { get; set; } = "";
        public string MangaUrl { get; set; } = "";
        public string ChapterUrl { get; set; } = "";
        public string ChapterName { get; set; } = "";
        public int ChapterId { get; set; }
        public int PageNumber { get; set; }
        public DateTime MarkedAt { get; set; }

        public string DisplayPage => $"Garis Baca: Halaman {PageNumber}";
        public string DisplayTime => MarkedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
    }

    public class MangaPageItem : INotifyPropertyChanged
    {
        public int PageNumber { get; set; }
        public string ImageUrl { get; set; } = "";
        public string EncryptionKeyHex { get; set; } = "";
        public bool IsSpread { get; set; } = false;
        public bool IsLastPage { get; set; } = false;

        private bool _isBookmarked = false;
        public bool IsBookmarked
        {
            get => _isBookmarked;
            set
            {
                if (_isBookmarked != value)
                {
                    _isBookmarked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBookmarked)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BookmarkVisibility)));
                }
            }
        }

        public Windows.UI.Xaml.Visibility BookmarkVisibility => _isBookmarked ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;

        public string BookmarkLabel => $"GARIS BACA • HALAMAN {PageNumber}";

        private BitmapImage _imageSource;
        public BitmapImage ImageSource
        {
            get => _imageSource;
            set
            {
                if (_imageSource != value)
                {
                    _imageSource = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageSource)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
