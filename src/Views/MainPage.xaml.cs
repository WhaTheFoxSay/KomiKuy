using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Mangaplus.Models;
using Mangaplus.Services;

namespace Mangaplus.Views
{
    public sealed partial class MainPage : Page
    {
        private readonly KomikuApiClient _api = KomikuApiClient.Instance;
        private readonly StorageService _storage = StorageService.Instance;
        private readonly DispatcherTimer _searchDebounceTimer;
        private bool _isFirstLaunch = true;

        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;

            _searchDebounceTimer = new DispatcherTimer();
            _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(500);
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            LoadBookmarks();

            if (e.NavigationMode == NavigationMode.Back)
            {
                // Preserve exact selected tab (e.g. MANHWA / MANGA / POPULER) and scroll position!
                ExtendedSplashPanel.Visibility = Visibility.Collapsed;
                return;
            }

            if (_isFirstLaunch)
            {
                _isFirstLaunch = false;
                await LaunchWithSplashAsync();
            }
            else
            {
                ExtendedSplashPanel.Visibility = Visibility.Collapsed;
                await LoadInitialDataAsync(false);
            }
        }

        private async Task LaunchWithSplashAsync()
        {
            ExtendedSplashPanel.Visibility = Visibility.Visible;
            if (SplashRing != null) SplashRing.IsActive = true;

            try
            {
                TxtSplashStatus.Text = "Menyiapkan KomiKuy!...";
                var cat = await _api.GetHomeCatalogAsync(false);
                if (cat != null)
                {
                    ApplyCatalogToUi(cat);
                }
            }
            catch { }
            finally
            {
                if (SplashFadeOutStoryboard != null)
                {
                    try { SplashFadeOutStoryboard.Begin(); } catch { }
                    await Task.Delay(250);
                }
                if (SplashRing != null) SplashRing.IsActive = false;
                ExtendedSplashPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadInitialDataAsync(bool forceRefresh)
        {
            TxtStatus.Text = "KomiKuy! • Menyelaraskan katalog...";

            try
            {
                // 1. Instant 0 ms local/cached catalog load
                var cat = await _api.GetHomeCatalogAsync(forceRefresh);

                if (cat != null)
                {
                    ApplyCatalogToUi(cat);
                }

                // 2. Background revalidation
                if (forceRefresh)
                {
                    var liveCat = await _api.RefreshHomeCatalogFromLiveApiAsync();
                    if (liveCat != null && liveCat.All.Count > 0)
                    {
                        ApplyCatalogToUi(liveCat);
                    }
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                TxtStatus.Text = "KomiKuy! • Siap Membaca";
            }
        }

        private void ApplyCatalogToUi(KomikuCategoriesResult cat)
        {
            if (cat == null) return;

            if (cat.Populer != null && cat.Populer.Count > 0)
                ListRankings.ItemsSource = cat.Populer;

            if (cat.Terbaru != null && cat.Terbaru.Count > 0)
                ListLatest.ItemsSource = cat.Terbaru;

            if (cat.Manga != null && cat.Manga.Count > 0)
                GridManga.ItemsSource = cat.Manga;

            if (cat.Manhwa != null && cat.Manhwa.Count > 0)
                GridManhwa.ItemsSource = cat.Manhwa;

            if (cat.Manhua != null && cat.Manhua.Count > 0)
                GridManhua.ItemsSource = cat.Manhua;

            EnsureGenresLoaded();

            TxtTotalCount.Text = $"{cat.All.Count} Komik";
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Memperbarui dari endpoint server...";
            await LoadInitialDataAsync(true);
            LoadBookmarks();
        }

        private void MangaItem_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MangaTitle title)
            {
                Frame.Navigate(typeof(TitleDetailPage), title);
            }
        }

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainPivot.SelectedIndex == 1) // LANJUTKAN TAB
            {
                LoadBookmarks();
            }
            else if (MainPivot.SelectedIndex == 6) // GENRE TAB
            {
                EnsureGenresLoaded();
            }
            else if (MainPivot.SelectedIndex == 8) // FAVORITES TAB
            {
                LoadFavorites();
            }
            else if (MainPivot.SelectedIndex == 9) // HISTORY TAB
            {
                LoadHistory();
            }
        }

        private List<GenreItem> _genreList;

        private void EnsureGenresLoaded()
        {
            if (_genreList != null && _genreList.Count > 0) return;

            _genreList = new List<GenreItem>
            {
                new GenreItem { Name = "Aksi", Slug = "action", Description = "Pertarungan & pertempuran seru", BadgeColor = "#EF4444" },
                new GenreItem { Name = "Petualangan", Slug = "adventure", Description = "Eksplorasi dunia & misi besar", BadgeColor = "#F59E0B" },
                new GenreItem { Name = "Isekai", Slug = "isekai", Description = "Pindah ke dunia lain & fantasi", BadgeColor = "#8B5CF6" },
                new GenreItem { Name = "Fantasi", Slug = "fantasy", Description = "Sihir, monster & kerajaan", BadgeColor = "#3B82F6" },
                new GenreItem { Name = "Bela Diri", Slug = "martial-arts", Description = "Kultivasi, wuxia & kungfu", BadgeColor = "#EC4899" },
                new GenreItem { Name = "Komedi", Slug = "comedy", Description = "Kisah lucu & menghibur", BadgeColor = "#10B981" },
                new GenreItem { Name = "Romantis", Slug = "romance", Description = "Kisah cinta & perasaan", BadgeColor = "#F43F5E" },
                new GenreItem { Name = "Drama", Slug = "drama", Description = "Konflik emosional & hubungan", BadgeColor = "#6366F1" },
                new GenreItem { Name = "Sci-Fi", Slug = "sci-fi", Description = "Teknologi masa depan & fiksi ilmiah", BadgeColor = "#06B6D4" },
                new GenreItem { Name = "Misteri", Slug = "mystery", Description = "Teka-teki & penyelidikan", BadgeColor = "#64748B" },
                new GenreItem { Name = "Supernatural", Slug = "supernatural", Description = "Kekuatan gaib & supranatural", BadgeColor = "#A855F7" },
                new GenreItem { Name = "Shounen", Slug = "shounen", Description = "Kisah perjuangan pemuda", BadgeColor = "#F97316" },
                new GenreItem { Name = "Reinkarnasi", Slug = "reincarnation", Description = "Lahir kembali di kehidupan baru", BadgeColor = "#14B8A6" },
                new GenreItem { Name = "Sihir", Slug = "magic", Description = "Mantra sihir & penyihir", BadgeColor = "#7C3AED" },
                new GenreItem { Name = "Sejarah", Slug = "historical", Description = "Kisah era kerajaan & zaman dulu", BadgeColor = "#B45309" },
                new GenreItem { Name = "Horor", Slug = "horror", Description = "Kisah seram & mendebarkan", BadgeColor = "#991B1B" },
                new GenreItem { Name = "Psikologis", Slug = "psychological", Description = "Permainan pikiran & psikologi", BadgeColor = "#475569" },
                new GenreItem { Name = "Olahraga", Slug = "sports", Description = "Pertandingan & kompetisi atletik", BadgeColor = "#15803D" },
                new GenreItem { Name = "Slice of Life", Slug = "slice-of-life", Description = "Keseharian yang menenangkan", BadgeColor = "#0D9488" },
                new GenreItem { Name = "School Life", Slug = "school-life", Description = "Kehidupan masa sekolah", BadgeColor = "#0284C7" }
            };

            GridGenres.ItemsSource = _genreList;
        }

        private async void GenreTile_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is GenreItem genre)
            {
                TxtCurrentGenreHeader.Text = $"GENRE: {genre.Name.ToUpperInvariant()}";
                PanelGenreSelector.Visibility = Visibility.Collapsed;
                PanelGenreComics.Visibility = Visibility.Visible;
                BarGenreLoading.Visibility = Visibility.Visible;
                ListGenreComics.ItemsSource = null;

                try
                {
                    var comics = await _api.GetComicsByGenreAsync(genre.Slug);
                    ListGenreComics.ItemsSource = comics;
                }
                catch { }
                finally
                {
                    BarGenreLoading.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void BtnBackToGenres_Click(object sender, RoutedEventArgs e)
        {
            PanelGenreComics.Visibility = Visibility.Collapsed;
            PanelGenreSelector.Visibility = Visibility.Visible;
        }

        private void LoadBookmarks()
        {
            var bookmarks = _storage.GetAllReadingBookmarks();
            if (bookmarks == null || bookmarks.Count == 0)
            {
                TxtNoBookmarks.Visibility = Visibility.Visible;
                ListBookmarks.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtNoBookmarks.Visibility = Visibility.Collapsed;
                ListBookmarks.Visibility = Visibility.Visible;
                ListBookmarks.ItemsSource = bookmarks;
            }
        }

        private void BookmarkItem_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ReadingBookmark bm)
            {
                var ch = new ChapterItem
                {
                    ChapterId = bm.ChapterId,
                    TitleId = bm.TitleId,
                    TitleName = bm.TitleName,
                    ChapterUrl = bm.ChapterUrl,
                    Name = bm.ChapterName,
                    ThumbnailUrl = bm.CoverUrl
                };

                var ctx = new ReaderContext
                {
                    CurrentChapter = ch,
                    AllChapters = new List<ChapterItem> { ch },
                    CurrentIndex = 0,
                    TargetPageNumber = bm.PageNumber // Scrolls directly to the bookmarked page!
                };

                Frame.Navigate(typeof(ReaderPage), ctx);
            }
        }

        private async void LoadFavorites()
        {
            var favIds = _storage.GetFavoriteIds();
            var favTitles = new List<MangaTitle>();
            
            var currentCatalog = await _api.GetHomeCatalogAsync(false);
            if (currentCatalog != null && currentCatalog.All != null)
            {
                var dict = currentCatalog.All.ToDictionary(t => t.TitleId, t => t);
                foreach (var id in favIds)
                {
                    if (dict.TryGetValue(id, out var t))
                        favTitles.Add(t);
                }
            }

            if (favTitles.Count == 0)
            {
                TxtNoFavorites.Visibility = Visibility.Visible;
                ListFavorites.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtNoFavorites.Visibility = Visibility.Collapsed;
                ListFavorites.Visibility = Visibility.Visible;
                ListFavorites.ItemsSource = favTitles;
            }
        }

        private void LoadHistory()
        {
            var histories = _storage.GetHistoryList();
            if (histories.Count == 0)
            {
                TxtNoHistory.Visibility = Visibility.Visible;
                ListHistory.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtNoHistory.Visibility = Visibility.Collapsed;
                ListHistory.Visibility = Visibility.Visible;
                ListHistory.ItemsSource = histories;
            }
        }

        private async void HistoryItem_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ReadingHistoryItem history)
            {
                string resolvedMangaUrl = history.MangaUrl;
                string coverUrl = history.CoverUrl;

                if (string.IsNullOrEmpty(resolvedMangaUrl) || resolvedMangaUrl.Contains("/manga/0/") || (resolvedMangaUrl.StartsWith("/manga/") && int.TryParse(resolvedMangaUrl.Trim('/', 'm', 'a', 'n', 'g'), out _)) || string.IsNullOrEmpty(coverUrl))
                {
                    // Fallback: match from catalog by TitleId or TitleName
                    try
                    {
                        var cat = await _api.GetHomeCatalogAsync(false);
                        var matched = cat?.All?.FirstOrDefault(t => t.TitleId == history.TitleId || (t.Name != null && t.Name.Equals(history.TitleName, StringComparison.OrdinalIgnoreCase)));
                        if (matched != null)
                        {
                            if (!string.IsNullOrEmpty(matched.MangaUrl)) resolvedMangaUrl = matched.MangaUrl;
                            if (string.IsNullOrEmpty(coverUrl) && !string.IsNullOrEmpty(matched.PortraitImageUrl)) coverUrl = matched.PortraitImageUrl;
                        }
                    }
                    catch { }
                }

                var title = new MangaTitle
                {
                    TitleId = history.TitleId,
                    Name = history.TitleName,
                    PortraitImageUrl = coverUrl,
                    MangaUrl = string.IsNullOrEmpty(resolvedMangaUrl) ? $"/manga/{history.TitleId}/" : resolvedMangaUrl
                };

                var navArgs = new TitleDetailNavigationArgs
                {
                    Title = title,
                    ResumeHistory = history
                };

                Frame.Navigate(typeof(TitleDetailPage), navArgs);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            string q = TxtSearch.Text.Trim();
            if (q.Length >= 2)
            {
                _searchDebounceTimer.Start();
            }
            else if (q.Length == 0)
            {
                ListSearchResults.ItemsSource = null;
                TxtStatus.Text = "KomiKuy!";
            }
        }

        private async void SearchDebounceTimer_Tick(object sender, object e)
        {
            _searchDebounceTimer.Stop();
            await PerformSearchAsync();
        }

        private async void BtnDoSearch_Click(object sender, RoutedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            await PerformSearchAsync();
        }

        private async void TxtSearch_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _searchDebounceTimer.Stop();
                await PerformSearchAsync();
            }
        }

        private async Task PerformSearchAsync()
        {
            string q = TxtSearch.Text.Trim();
            if (string.IsNullOrEmpty(q)) return;

            TxtStatus.Text = $"Mencari '{q}'...";
            try
            {
                var results = await _api.SearchMangaAsync(q);
                ListSearchResults.ItemsSource = results;
                TxtStatus.Text = results.Count > 0 ? $"Ditemukan {results.Count} komik" : $"Tidak ada hasil untuk '{q}'";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Gagal mencari: {ex.Message}";
            }
        }
    }
}
