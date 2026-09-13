using System;
using Windows.Graphics.Display;
using Windows.UI.Xaml;

namespace Mangaplus.Helpers
{
    public static class DeviceScreenHelper
    {
        public static double GetScreenWidth()
        {
            try
            {
                if (Window.Current != null && Window.Current.Bounds.Width > 0)
                    return Window.Current.Bounds.Width;
            }
            catch { }
            return 360.0;
        }

        public static int GetOptimalPixelWidth()
        {
            try
            {
                var display = DisplayInformation.GetForCurrentView();
                double scale = (double)display.ResolutionScale / 100.0;
                if (scale < 1.0) scale = 1.0;

                double logicalWidth = GetScreenWidth();

                // Exact physical hardware pixel width of any Lumia phone:
                // - Lumia 520/530/535/625/630 (WVGA): 480 px
                // - Lumia 720/640/730/830 (HD): 720 px
                // - Lumia 920/925/928/1020 (WXGA): 768 px
                // - Lumia 930/1520/Icon (Full HD): 1080 px
                // - Lumia 950/950 XL (WQHD): 1440 px
                int physicalWidth = (int)Math.Round(logicalWidth * scale);
                return Math.Max(480, physicalWidth);
            }
            catch
            {
                return 480;
            }
        }
    }
}
