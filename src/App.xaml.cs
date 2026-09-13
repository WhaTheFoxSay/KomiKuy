using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Mangaplus.Views;

namespace Mangaplus
{
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Lock display rotation to Portrait on Windows 10 Mobile
            try
            {
                Windows.Graphics.Display.DisplayInformation.AutoRotationPreferences =
                    Windows.Graphics.Display.DisplayOrientations.Portrait |
                    Windows.Graphics.Display.DisplayOrientations.PortraitFlipped;
            }
            catch { }

            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;

                // Register Windows 10 Mobile Software & Hardware Back Navigation
                SystemNavigationManager.GetForCurrentView().BackRequested += (s, args) =>
                {
                    if (rootFrame.CanGoBack)
                    {
                        args.Handled = true;
                        rootFrame.GoBack();
                    }
                };

                if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
                {
                    Windows.Phone.UI.Input.HardwareButtons.BackPressed += (s, args) =>
                    {
                        if (rootFrame.CanGoBack)
                        {
                            args.Handled = true;
                            rootFrame.GoBack();
                        }
                    };
                }

                // Configure Mobile Status Bar
                try
                {
                    if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                    {
                        var statusBar = StatusBar.GetForCurrentView();
                        if (statusBar != null)
                        {
                            statusBar.BackgroundColor = Color.FromArgb(255, 20, 78, 19);
                            statusBar.ForegroundColor = Colors.White;
                            statusBar.BackgroundOpacity = 1;
                        }
                    }
                }
                catch { }

                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}
