using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Mangaplus.Models;
using Mangaplus.Services;

namespace Mangaplus.Views
{
    public sealed partial class TitleDetailPage : Page
    {
        private readonly KomikuApiClient _api = KomikuApiClient.Instance;
        private readonly StorageService _storage = StorageService.Instance;

        private MangaTitle _title;
        private ReadingHistoryItem _resumeHistory = null;
        private List<ChapterItem> _allChapters = new List<ChapterItem>();
        private List<ChapterRangeTag> _rangeTags = new List<ChapterRangeTag>();
        private ChapterRangeTag _selectedRangeTag = null;
        private bool _isReverseSort = false;
        private bool _isSynopsisExpanded = false;
        private readonly DispatcherTimer _chapterSearchDebounceTimer;

        public TitleDetailPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;

            _chapterSearchDebounceTimer = new DispatcherTimer();
            _chapterSearchDebounceTimer.Interval = TimeSpan.FromMilliseconds(1500);
            _chapterSearchDebounceTimer.Tick += ChapterSearchDebounceTimer_Tick;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.Back)
            {
                // Return from Reader or another page:
                // 1. Always dismiss the resume modal so detail is revealed
                PanelResumeModal.Visibility = Visibility.Collapsed;

                // 2. Restore cover image if it was lost
                if (ImgCover.Source == null && _title != null && !string.IsNullOrEmpty(_title.PortraitImageUrl))
                {
                    try
                    {
                        var bmp = new BitmapImage(new Uri(_title.PortraitImageUrl)) { DecodePixelWidth = 200 };
                        ImgCover.Source = bmp;
                    }
                    catch { }
                }

                // 3. If details or chapters were interrupted, reload
                if (_allChapters == null || _allChapters.Count == 0)
                {
                    _ = LoadDetailAsync(_title?.MangaUrl);
                }
                return;
            }

            MangaTitle newTitle = null;
            if (e.Parameter is TitleDetailNavigationArgs navArgs)
            {
                newTitle = navArgs.Title;
                _resumeHistory = navArgs.ResumeHistory;
            }
            else if (e.Parameter is MangaTitle t)
            {
                newTitle = t;
                _resumeHistory = null;
            }

            if (newTitle == null) return;

            // If navigating to a different comic, reset all state to top
            _title = newTitle;
            _allChapters = new List<ChapterItem>();
            _rangeTags = new List<ChapterRangeTag>();
            _selectedRangeTag = null;
            _isReverseSort = false;
            _isSynopsisExpanded = false;
            _chapterSearchDebounceTimer.Stop();
            if (TxtSearchChapter != null) TxtSearchChapter.Text = "";
            if (BtnClearChapterSearch != null) BtnClearChapterSearch.Visibility = Visibility.Collapsed;
            if (BtnToggleSynopsis != null) BtnToggleSynopsis.Visibility = Visibility.Collapsed;
            if (RowLastUpdate != null) RowLastUpdate.Visibility = Visibility.Collapsed;
            ListChapters.ItemsSource = null;

            TxtHeaderTitle.Text = _title.Name.ToUpperInvariant();
            TxtTitleName.Text = _title.Name;
            TxtAuthor.Text = string.IsNullOrEmpty(_title.Author) ? "-" : _title.Author;
            TxtComicType.Text = _title.DisplayType;
            TxtStatusBadge.Text = _title.DisplayStatus;

            if (!string.IsNullOrEmpty(_title.PortraitImageUrl))
            {
                try
                {
                    var bmp = new BitmapImage(new Uri(_title.PortraitImageUrl)) { DecodePixelWidth = 200 };
                    ImgCover.Source = bmp;
                }
                catch { }
            }

            if (_resumeHistory != null && !string.IsNullOrEmpty(_resumeHistory.ChapterName))
            {
                TxtResumePromptTitle.Text = "Lanjut Membaca?";
                TxtResumePromptChapter.Text = $"Bab sebelumnya:\n{_resumeHistory.ChapterName}";
                PanelResumeModal.Visibility = Visibility.Visible;
            }
            else
            {
                PanelResumeModal.Visibility = Visibility.Collapsed;
            }

            UpdateFavoriteState();
            await LoadDetailAsync(_title.MangaUrl);
        }

        private async Task LoadDetailAsync(string mangaUrl)
        {
            BarLoading.Visibility = Visibility.Visible;
            try
            {
                var res = await _api.GetMangaDetailAsync(mangaUrl);
                var detail = res.Detail;
                var chapters = res.Chapters;

                if (detail != null)
                {
                    // 1. Title and Alternative Title
                    if (!string.IsNullOrEmpty(detail.Name))
                    {
                        TxtHeaderTitle.Text = detail.Name.ToUpperInvariant();
                        TxtTitleName.Text = detail.Name;
                    }

                    if (!string.IsNullOrEmpty(detail.AlternativeName))
                    {
                        TxtAltName.Text = detail.AlternativeName;
                        TxtAltName.Visibility = Visibility.Visible;
                    }

                    // 2. Author, Type, Status
                    if (!string.IsNullOrEmpty(detail.Author))
                        TxtAuthor.Text = detail.Author;

                    if (!string.IsNullOrEmpty(detail.ComicType))
                        TxtComicType.Text = detail.ComicType;

                    if (!string.IsNullOrEmpty(detail.Status))
                        TxtStatusBadge.Text = detail.Status;

                    // 3. Rating & Views
                    if (!string.IsNullOrEmpty(detail.Rating))
                    {
                        TxtRating.Text = detail.Rating;
                        RowRating.Visibility = Visibility.Visible;
                    }

                    if (!string.IsNullOrEmpty(detail.ViewCount))
                    {
                        TxtViews.Text = detail.ViewCount;
                        RowViews.Visibility = Visibility.Visible;
                    }

                    // 4. Cover
                    string coverToUse = !string.IsNullOrEmpty(detail.PortraitImageUrl) ? detail.PortraitImageUrl : _title?.PortraitImageUrl;
                    if (!string.IsNullOrEmpty(coverToUse))
                    {
                        if (_title != null) _title.PortraitImageUrl = coverToUse;
                        try
                        {
                            var bmp = new BitmapImage(new Uri(coverToUse)) { DecodePixelWidth = 200 };
                            ImgCover.Source = bmp;
                        }
                        catch { }
                    }

                    // 5. Genres
                    if (detail.Genres != null && detail.Genres.Count > 0)
                    {
                        ListGenreChips.Children.Clear();
                        foreach (var g in detail.Genres)
                        {
                            var border = new Border
                            {
                                Background = new SolidColorBrush(Color.FromArgb(255, 30, 35, 48)),
                                CornerRadius = new CornerRadius(3),
                                Padding = new Thickness(6, 2, 6, 2),
                                Margin = new Thickness(0, 0, 4, 0)
                            };
                            var tb = new TextBlock
                            {
                                Text = g,
                                FontSize = 9,
                                Foreground = new SolidColorBrush(Color.FromArgb(255, 147, 197, 253)),
                                FontWeight = Windows.UI.Text.FontWeights.SemiBold
                            };
                            border.Child = tb;
                            ListGenreChips.Children.Add(border);
                        }
                        PanelGenres.Visibility = Visibility.Visible;
                    }

                    // 6. Synopsis (Short with Toggle to Full)
                    if (!string.IsNullOrWhiteSpace(detail.Synopsis))
                    {
                        TxtSynopsis.Text = detail.Synopsis;
                        TxtSynopsis.MaxLines = 3;
                        _isSynopsisExpanded = false;
                        TxtToggleSynopsis.Text = "Buka sinopsis lengkap ▼";
                        BtnToggleSynopsis.Visibility = detail.Synopsis.Length > 80 ? Visibility.Visible : Visibility.Collapsed;
                    }
                    else
                    {
                        TxtSynopsis.Text = "Sinopsis belum tersedia.";
                        BtnToggleSynopsis.Visibility = Visibility.Collapsed;
                    }

                    // 7. Chapters
                    _allChapters = chapters ?? new List<ChapterItem>();
                    TxtTotalChapters.Text = $"{_allChapters.Count} Chapter";

                    foreach (var c in _allChapters)
                    {
                        if (string.IsNullOrEmpty(c.MangaUrl)) c.MangaUrl = _title?.MangaUrl ?? "";
                        if (string.IsNullOrEmpty(c.TitleName)) c.TitleName = _title?.Name ?? "";
                        if (string.IsNullOrEmpty(c.ThumbnailUrl)) c.ThumbnailUrl = _title?.PortraitImageUrl ?? "";
                    }

                    // 8. Ongoing Last Update Info (Hari, Tanggal, Jam atau Jadwal)
                    if (detail.IsOngoing)
                    {
                        var latest = _allChapters.OrderByDescending(c => c.ChapterNumber).FirstOrDefault();
                        string updateInfo = "";
                        if (latest != null && !string.IsNullOrEmpty(latest.ReleaseDateString))
                        {
                            updateInfo = $"{latest.ReleaseDateString} ({latest.DisplayName})";
                        }
                        else if (!string.IsNullOrEmpty(detail.UpdatedTimeAgo))
                        {
                            updateInfo = detail.UpdatedTimeAgo;
                        }
                        else if (latest != null)
                        {
                            updateInfo = latest.DisplayName;
                        }

                        if (!string.IsNullOrEmpty(updateInfo))
                        {
                            TxtLastUpdate.Text = updateInfo;
                            RowLastUpdate.Visibility = Visibility.Visible;
                        }
                    }

                    BuildDynamicRangeTabs();
                    ApplyChapterDisplay();
                }
            }
            catch (Exception ex)
            {
                TxtSynopsis.Text = $"Gagal memuat detail komik: {ex.Message}";
            }
            finally
            {
                BarLoading.Visibility = Visibility.Collapsed;
            }
        }

        private void BuildDynamicRangeTabs()
        {
            PanelRangeTags.Children.Clear();
            _rangeTags.Clear();

            if (_allChapters == null || _allChapters.Count == 0)
            {
                _selectedRangeTag = null;
                ListChapters.ItemsSource = null;
                return;
            }

            double minCh = _allChapters.Min(c => c.ChapterNumber);
            double maxCh = _allChapters.Max(c => c.ChapterNumber);

            int minFloor = (int)Math.Floor(minCh);
            int maxCeil = (int)Math.Ceiling(maxCh);

            int rangeSize = 50;
            int start = (minFloor / rangeSize) * rangeSize + 1;
            if (start > minFloor) start = Math.Max(1, start - rangeSize);

            // Generate range tags matching actual chapters
            while (start <= maxCeil)
            {
                int end = start + rangeSize - 1;
                if (end > maxCeil && end < maxCeil + rangeSize - 5) end = maxCeil;

                var tag = new ChapterRangeTag
                {
                    Label = $"{start}-{end}",
                    MinChapter = start,
                    MaxChapter = end,
                    IsSelected = _rangeTags.Count == 0
                };
                _rangeTags.Add(tag);

                start += rangeSize;
            }

            // Always add "SEMUA" tab
            _rangeTags.Add(new ChapterRangeTag
            {
                Label = "SEMUA",
                MinChapter = 0,
                MaxChapter = 999999,
                IsSelected = false
            });

            _selectedRangeTag = _rangeTags.FirstOrDefault(t => t.IsSelected) ?? _rangeTags.FirstOrDefault();

            // Render Segmented Metro Tabs
            foreach (var tag in _rangeTags)
            {
                bool isSel = (tag == _selectedRangeTag);

                var stack = new StackPanel
                {
                    Margin = new Thickness(0, 0, 14, 0)
                };

                var tb = new TextBlock
                {
                    Text = tag.Label,
                    FontSize = 11.5,
                    FontWeight = isSel ? Windows.UI.Text.FontWeights.Bold : Windows.UI.Text.FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = isSel ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb(255, 142, 149, 165)),
                    Margin = new Thickness(2, 0, 2, 3)
                };
                stack.Children.Add(tb);

                var indicator = new Border
                {
                    Height = 2.5,
                    Background = isSel ? new SolidColorBrush(Color.FromArgb(255, 37, 99, 235)) : new SolidColorBrush(Colors.Transparent)
                };
                stack.Children.Add(indicator);

                var btn = new Button
                {
                    Content = stack,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    Tag = tag,
                    MinWidth = 0,
                    MinHeight = 0
                };
                btn.Click += MetroRangeTab_Click;

                PanelRangeTags.Children.Add(btn);
            }
        }

        private void MetroRangeTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ChapterRangeTag tag)
            {
                if (_selectedRangeTag == tag) return;
                _selectedRangeTag = tag;

                foreach (var child in PanelRangeTags.Children)
                {
                    if (child is Button b && b.Tag is ChapterRangeTag t && b.Content is StackPanel sp)
                    {
                        bool isSel = (t == _selectedRangeTag);
                        if (sp.Children.Count >= 2)
                        {
                            if (sp.Children[0] is TextBlock tb)
                            {
                                tb.FontWeight = isSel ? Windows.UI.Text.FontWeights.Bold : Windows.UI.Text.FontWeights.SemiBold;
                                tb.Foreground = isSel ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Color.FromArgb(255, 142, 149, 165));
                            }
                            if (sp.Children[1] is Border ind)
                            {
                                ind.Background = isSel ? new SolidColorBrush(Color.FromArgb(255, 37, 99, 235)) : new SolidColorBrush(Colors.Transparent);
                            }
                        }
                    }
                }

                ApplyChapterDisplay();
            }
        }

        private void ApplyChapterDisplay()
        {
            if (_allChapters == null || _allChapters.Count == 0)
            {
                ListChapters.ItemsSource = null;
                return;
            }

            IEnumerable<ChapterItem> chapters = _allChapters;

            if (_selectedRangeTag != null && _selectedRangeTag.Label != "SEMUA")
            {
                chapters = _allChapters.Where(c => c.ChapterNumber >= _selectedRangeTag.MinChapter && c.ChapterNumber <= _selectedRangeTag.MaxChapter);
            }

            List<ChapterItem> chapterList;
            if (_isReverseSort)
            {
                chapterList = chapters.OrderByDescending(c => c.ChapterNumber).ToList();
            }
            else
            {
                chapterList = chapters.OrderBy(c => c.ChapterNumber).ToList();
            }

            ListChapters.ItemsSource = chapterList;
        }

        private void UpdateFavoriteState()
        {
            if (_title == null) return;
            bool isFav = _storage.IsFavorite(_title.TitleId);
            TxtFavIcon.Text = isFav ? "★" : "☆";
        }

        private void BtnFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (_title == null) return;
            _storage.ToggleFavorite(_title.TitleId);
            UpdateFavoriteState();
        }

        private void BtnSortChapters_Click(object sender, RoutedEventArgs e)
        {
            _isReverseSort = !_isReverseSort;
            ApplyChapterDisplay();
        }

        private void BtnFirstChapter_Click(object sender, RoutedEventArgs e)
        {
            if (_allChapters != null && _allChapters.Count > 0)
            {
                var sorted = _allChapters.OrderBy(c => c.ChapterNumber).ToList();
                var ctx = new ReaderContext
                {
                    CurrentChapter = sorted[0],
                    AllChapters = sorted,
                    CurrentIndex = 0
                };
                Frame.Navigate(typeof(ReaderPage), ctx);
            }
        }

        private void BtnLatestChapter_Click(object sender, RoutedEventArgs e)
        {
            if (_allChapters != null && _allChapters.Count > 0)
            {
                var sorted = _allChapters.OrderBy(c => c.ChapterNumber).ToList();
                int lastIdx = sorted.Count - 1;
                var ctx = new ReaderContext
                {
                    CurrentChapter = sorted[lastIdx],
                    AllChapters = sorted,
                    CurrentIndex = lastIdx
                };
                Frame.Navigate(typeof(ReaderPage), ctx);
            }
        }

        private void ChapterItem_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChapterItem ch)
            {
                var ordered = _allChapters.OrderBy(c => c.ChapterNumber).ToList();
                int idx = ordered.FindIndex(c => c.ChapterUrl == ch.ChapterUrl);
                var ctx = new ReaderContext
                {
                    CurrentChapter = ch,
                    AllChapters = ordered,
                    CurrentIndex = Math.Max(0, idx)
                };
                Frame.Navigate(typeof(ReaderPage), ctx);
            }
        }

        private void BtnToggleSynopsis_Click(object sender, RoutedEventArgs e)
        {
            _isSynopsisExpanded = !_isSynopsisExpanded;
            if (_isSynopsisExpanded)
            {
                TxtSynopsis.MaxLines = 0;
                TxtToggleSynopsis.Text = "Perkecil sinopsis ▲";
            }
            else
            {
                TxtSynopsis.MaxLines = 3;
                TxtToggleSynopsis.Text = "Buka sinopsis lengkap ▼";
            }
        }

        private void TxtSearchChapter_TextChanged(object sender, TextChangedEventArgs e)
        {
            _chapterSearchDebounceTimer.Stop();
            _chapterSearchDebounceTimer.Start();
            BtnClearChapterSearch.Visibility = string.IsNullOrEmpty(TxtSearchChapter.Text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ChapterSearchDebounceTimer_Tick(object sender, object e)
        {
            _chapterSearchDebounceTimer.Stop();
            ApplyChapterSearchFilter();
        }

        private void BtnClearChapterSearch_Click(object sender, RoutedEventArgs e)
        {
            _chapterSearchDebounceTimer.Stop();
            TxtSearchChapter.Text = "";
            BtnClearChapterSearch.Visibility = Visibility.Collapsed;
            ApplyChapterDisplay();
        }

        private void ApplyChapterSearchFilter()
        {
            string q = TxtSearchChapter.Text.Trim();
            if (string.IsNullOrEmpty(q))
            {
                ApplyChapterDisplay();
                return;
            }

            if (_allChapters == null || _allChapters.Count == 0) return;

            var matched = _allChapters.Where(c =>
                c.ChapterNumber.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(q) ||
                (c.Name != null && c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();

            if (_isReverseSort)
                matched = matched.OrderByDescending(c => c.ChapterNumber).ToList();
            else
                matched = matched.OrderBy(c => c.ChapterNumber).ToList();

            ListChapters.ItemsSource = matched;
            TxtTotalChapters.Text = $"{matched.Count} Chapter ditemukan";
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _chapterSearchDebounceTimer.Stop();
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainPage));
        }

        private void BtnDismissResume_Click(object sender, RoutedEventArgs e)
        {
            PanelResumeModal.Visibility = Visibility.Collapsed;
        }

        private void BtnConfirmResume_Click(object sender, RoutedEventArgs e)
        {
            PanelResumeModal.Visibility = Visibility.Collapsed;

            if (_resumeHistory != null && !string.IsNullOrEmpty(_resumeHistory.ChapterUrl))
            {
                var ch = _allChapters?.FirstOrDefault(c => c.ChapterUrl == _resumeHistory.ChapterUrl || c.Name == _resumeHistory.ChapterName);
                if (ch == null)
                {
                    ch = new ChapterItem
                    {
                        ChapterId = _resumeHistory.ChapterId,
                        TitleId = _resumeHistory.TitleId,
                        TitleName = _resumeHistory.TitleName,
                        MangaUrl = string.IsNullOrEmpty(_title?.MangaUrl) ? _resumeHistory.MangaUrl : _title.MangaUrl,
                        ThumbnailUrl = string.IsNullOrEmpty(_title?.PortraitImageUrl) ? _resumeHistory.CoverUrl : _title.PortraitImageUrl,
                        ChapterUrl = _resumeHistory.ChapterUrl,
                        Name = _resumeHistory.ChapterName
                    };
                }

                var ordered = _allChapters != null && _allChapters.Count > 0 ? _allChapters.OrderBy(c => c.ChapterNumber).ToList() : new List<ChapterItem> { ch };
                int idx = ordered.FindIndex(c => c.ChapterUrl == ch.ChapterUrl);
                var ctx = new ReaderContext
                {
                    CurrentChapter = ch,
                    AllChapters = ordered,
                    CurrentIndex = Math.Max(0, idx)
                };
                Frame.Navigate(typeof(ReaderPage), ctx);
            }
        }
    }
}
