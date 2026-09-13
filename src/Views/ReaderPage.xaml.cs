using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Mangaplus.Models;
using Mangaplus.Services;

namespace Mangaplus.Views
{
    public sealed partial class ReaderPage : Page
    {
        private readonly KomikuApiClient _api = KomikuApiClient.Instance;
        private readonly MangaImageDecryptor _decryptor = MangaImageDecryptor.Instance;
        private readonly StorageService _storage = StorageService.Instance;

        private ReaderContext _context;
        private List<MangaPageItem> _pages = new List<MangaPageItem>();
        private bool _controlsVisible = true;
        private bool _swipeUpDismissed = false;
        private bool _isPageActive = false;
        private bool _isMarkingBookmarkMode = false;

        public ReaderPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _isPageActive = true;
            _isMarkingBookmarkMode = false;

            if (e.Parameter is ReaderContext ctx)
            {
                _context = ctx;
            }
            else if (e.Parameter is ChapterItem item)
            {
                _context = new ReaderContext
                {
                    CurrentChapter = item,
                    AllChapters = new List<ChapterItem> { item },
                    CurrentIndex = 0
                };
            }

            if (_context?.CurrentChapter != null)
            {
                ShowSwipeUpAnimation();
                await LoadCurrentChapterAsync();
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            _isPageActive = false;
            _isMarkingBookmarkMode = false;
            base.OnNavigatingFrom(e);
        }

        private void ShowSwipeUpAnimation()
        {
            _swipeUpDismissed = false;
            PanelSwipeUpOverlay.Visibility = Visibility.Visible;
            try
            {
                SwipeUpStoryboard.Begin();
            }
            catch { }
        }

        private void DismissSwipeUpOverlay()
        {
            if (_swipeUpDismissed) return;
            _swipeUpDismissed = true;
            try
            {
                SwipeUpStoryboard.Stop();
            }
            catch { }
            PanelSwipeUpOverlay.Visibility = Visibility.Collapsed;
        }

        private void SwipeUpOverlay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            DismissSwipeUpOverlay();
        }

        private void SwipeUpOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            DismissSwipeUpOverlay();
        }

        private async Task LoadCurrentChapterAsync()
        {
            if (_context?.CurrentChapter == null) return;

            PanelLockedNotice.Visibility = Visibility.Collapsed;
            var ch = _context.CurrentChapter;
            TxtChapterTitle.Text = ch.DisplayName;
            TxtMangaTitle.Text = ch.TitleName;

            UpdateNavigationButtons();

            try
            {
                if (ListPages.Items.Count > 0)
                    ListPages.ScrollIntoView(ListPages.Items[0]);
            }
            catch { }

            // Trigger background prefetch of next & prev chapters immediately
            TriggerNextPrevPrefetch();

            await LoadPagesAsync(ch.ChapterUrl);
        }

        private void TriggerNextPrevPrefetch()
        {
            if (_context == null || _context.AllChapters == null) return;

            // Prefetch Next chapter
            if (_context.HasNext && _context.CurrentIndex + 1 < _context.AllChapters.Count)
            {
                var nextCh = _context.AllChapters[_context.CurrentIndex + 1];
                _api.PrefetchChapterInBackground(nextCh.ChapterUrl);
            }

            // Prefetch Prev chapter
            if (_context.HasPrevious && _context.CurrentIndex - 1 >= 0)
            {
                var prevCh = _context.AllChapters[_context.CurrentIndex - 1];
                _api.PrefetchChapterInBackground(prevCh.ChapterUrl);
            }
        }

        private void UpdateNavigationButtons()
        {
            if (_context != null)
            {
                BtnPrevChapter.IsEnabled = _context.HasPrevious;
                BtnPrevChapter.Opacity = _context.HasPrevious ? 1.0 : 0.35;

                BtnNextChapter.IsEnabled = _context.HasNext;
                BtnNextChapter.Opacity = _context.HasNext ? 1.0 : 0.35;
            }
        }

        private async Task LoadPagesAsync(string chapterUrl)
        {
            BarLoading.Visibility = Visibility.Visible;
            ListPages.ItemsSource = null;

            try
            {
                _pages = await _api.GetChapterPagesAsync(chapterUrl);
                TxtPageCount.Text = $"{_pages.Count} Hal";

                if (_pages.Count == 0)
                {
                    BarLoading.Visibility = Visibility.Collapsed;
                    PanelLockedNotice.Visibility = Visibility.Visible;
                    return;
                }

                PanelLockedNotice.Visibility = Visibility.Collapsed;

                var currentCh = _context.CurrentChapter;

                _storage.RecordReading(
                    currentCh.TitleId,
                    currentCh.TitleName,
                    currentCh.ThumbnailUrl,
                    currentCh.MangaUrl,
                    currentCh.ChapterId,
                    currentCh.DisplayName,
                    currentCh.ChapterUrl,
                    1,
                    _pages.Count);

                // Check if there is an active reading bookmark on this specific chapter
                var activeBm = _storage.GetReadingBookmark(currentCh.TitleId, currentCh.TitleName);
                int targetPage = _context.TargetPageNumber;

                if (activeBm != null && (activeBm.ChapterUrl == currentCh.ChapterUrl || activeBm.ChapterName == currentCh.DisplayName))
                {
                    foreach (var page in _pages)
                    {
                        if (page.PageNumber == activeBm.PageNumber)
                        {
                            page.IsBookmarked = true;
                            if (targetPage == 0) targetPage = activeBm.PageNumber;
                        }
                    }
                }

                // Bind to virtualized ListView
                ListPages.ItemsSource = _pages;

                // Scroll to bookmarked page if navigated from "Lanjutkan"
                if (targetPage > 0 && targetPage <= _pages.Count)
                {
                    try
                    {
                        ListPages.ScrollIntoView(_pages[targetPage - 1]);
                    }
                    catch { }
                }

                // 1. Render first 2 pages immediately
                int initialBatch = Math.Min(2, _pages.Count);
                for (int i = 0; i < initialBatch; i++)
                {
                    await LoadSinglePageAsync(_pages[i]);
                }

                BarLoading.Visibility = Visibility.Collapsed;

                // 2. Prefetch next chapter in background
                if (_context != null && _context.HasNext)
                {
                    int nextIdx = _context.CurrentIndex + 1;
                    if (nextIdx < _context.AllChapters.Count)
                    {
                        var nextCh = _context.AllChapters[nextIdx];
                        if (nextCh != null && !string.IsNullOrEmpty(nextCh.ChapterUrl))
                        {
                            _api.PrefetchChapterInBackground(nextCh.ChapterUrl);
                        }
                    }
                }

                // 3. Render remaining pages progressively with background parallel download (zero UI blocking)
                _ = Task.Run(async () =>
                {
                    using (var throttler = new System.Threading.SemaphoreSlim(3, 3))
                    {
                        var downloadTasks = new List<Task>();
                        for (int i = initialBatch; i < _pages.Count; i++)
                        {
                            if (!_isPageActive) break;
                            var page = _pages[i];

                            await throttler.WaitAsync();
                            downloadTasks.Add(Task.Run(async () =>
                            {
                                try
                                {
                                    if (!_isPageActive || page.ImageSource != null) return;
                                    var bytes = await _decryptor.GetImageBytesAsync(page.ImageUrl, "");
                                    if (bytes != null && bytes.Length > 0 && _isPageActive)
                                    {
                                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                                            Windows.UI.Core.CoreDispatcherPriority.Low,
                                            async () =>
                                            {
                                                if (_isPageActive && page.ImageSource == null)
                                                {
                                                    try
                                                    {
                                                        var stream = new InMemoryRandomAccessStream();
                                                        await stream.WriteAsync(bytes.AsBuffer());
                                                        stream.Seek(0);
                                                        var bmp = new BitmapImage { DecodePixelWidth = 540 };
                                                        await bmp.SetSourceAsync(stream);
                                                        page.ImageSource = bmp;
                                                    }
                                                    catch { }
                                                }
                                            });
                                    }
                                }
                                catch { }
                                finally
                                {
                                    throttler.Release();
                                }
                            }));
                        }
                        await Task.WhenAll(downloadTasks);
                    }
                });
            }
            catch
            {
                BarLoading.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadSinglePageAsync(MangaPageItem page)
        {
            try
            {
                if (page.ImageSource == null)
                {
                    var bytes = await _decryptor.GetImageBytesAsync(page.ImageUrl, "");
                    if (bytes != null && bytes.Length > 0)
                    {
                        var stream = new InMemoryRandomAccessStream();
                        await stream.WriteAsync(bytes.AsBuffer());
                        stream.Seek(0);
                        var bmp = new BitmapImage { DecodePixelWidth = 540 };
                        await bmp.SetSourceAsync(stream);
                        page.ImageSource = bmp;
                    }
                }
            }
            catch { }
        }

        private void BtnToggleBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (_pages == null || _pages.Count == 0) return;

            _isMarkingBookmarkMode = !_isMarkingBookmarkMode;

            if (_isMarkingBookmarkMode)
            {
                TxtBtnBookmark.Text = "Pilih Hal...";
                ShowToastBookmark("Ketuk pada halaman komik yang ingin Anda tandai!");
            }
            else
            {
                TxtBtnBookmark.Text = "Garis Baca";
            }
        }

        private void PageItem_Tapped(object sender, TappedRoutedEventArgs e)
        {
            DismissSwipeUpOverlay();

            if (_isMarkingBookmarkMode)
            {
                if (sender is FrameworkElement elem && elem.DataContext is MangaPageItem targetPage)
                {
                    MarkBookmarkAtPage(targetPage.PageNumber);
                    _isMarkingBookmarkMode = false;
                    TxtBtnBookmark.Text = "Garis Baca";
                    return;
                }
            }

            // Normal tap toggles top/bottom bars
            _controlsVisible = !_controlsVisible;
            TopBar.Visibility = _controlsVisible ? Visibility.Visible : Visibility.Collapsed;
            BottomBar.Visibility = _controlsVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MarkBookmarkAtPage(int pageNumber)
        {
            if (_context?.CurrentChapter == null || _pages == null || _pages.Count == 0) return;

            var currentCh = _context.CurrentChapter;

            // Clear previous bookmarks in this chapter list
            foreach (var p in _pages)
            {
                p.IsBookmarked = false;
            }

            // Mark the chosen page
            if (pageNumber > 0 && pageNumber <= _pages.Count)
            {
                _pages[pageNumber - 1].IsBookmarked = true;
            }

            int tid = currentCh.TitleId;
            if (tid == 0)
            {
                tid = Math.Abs((currentCh.TitleName ?? currentCh.ChapterUrl ?? "manga").GetHashCode());
            }

            // Save to StorageService (Replaces any previous bookmark on this comic)
            var bm = new ReadingBookmark
            {
                TitleId = tid,
                TitleName = currentCh.TitleName,
                CoverUrl = currentCh.ThumbnailUrl,
                MangaUrl = $"/manga/{tid}/",
                ChapterUrl = currentCh.ChapterUrl,
                ChapterName = currentCh.DisplayName,
                ChapterId = currentCh.ChapterId,
                PageNumber = pageNumber,
                MarkedAt = DateTime.UtcNow
            };
            _storage.SetReadingBookmark(bm);

            ShowToastBookmark($"Garis baca ditandai di {currentCh.DisplayName} Hal {pageNumber}!");
        }

        private void ShowToastBookmark(string msg)
        {
            TxtToastMessage.Text = msg;
            PanelToastBookmark.Visibility = Visibility.Visible;
            Task.Run(async () =>
            {
                await Task.Delay(2500);
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () => { PanelToastBookmark.Visibility = Visibility.Collapsed; });
            });
        }

        private async void BtnPrevChapter_Click(object sender, RoutedEventArgs e)
        {
            if (_context != null && _context.HasPrevious)
            {
                _context.CurrentIndex--;
                _context.CurrentChapter = _context.AllChapters[_context.CurrentIndex];
                _context.TargetPageNumber = 0;
                _isMarkingBookmarkMode = false;
                TxtBtnBookmark.Text = "Garis Baca";
                ShowSwipeUpAnimation();
                await LoadCurrentChapterAsync();
            }
        }

        private async void BtnNextChapter_Click(object sender, RoutedEventArgs e)
        {
            if (_context != null && _context.HasNext)
            {
                _context.CurrentIndex++;
                _context.CurrentChapter = _context.AllChapters[_context.CurrentIndex];
                _context.TargetPageNumber = 0;
                _isMarkingBookmarkMode = false;
                TxtBtnBookmark.Text = "Garis Baca";
                ShowSwipeUpAnimation();
                await LoadCurrentChapterAsync();
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(MainPage));
        }
    }
}
