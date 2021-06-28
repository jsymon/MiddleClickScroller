using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace MiddleClickScroller
{
    internal static class CursorImages
    {
        internal static Image BuildScrollAllImage()
        {
            //IMAGE_CURSOR      LR_CREATEDDIBSECTION   LR_SHARED
            IntPtr hScrollAllCursor = User32.LoadImage(IntPtr.Zero, new IntPtr(32512 + 142), (uint)2, 0, 0, (uint)(0x00002000 | 0x00008000));
            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(hScrollAllCursor, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();

            Image zeroPointImage = new Image() { Source = source, Opacity = 0.5 };
            zeroPointImage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return zeroPointImage;
        }

        internal static class User32
        {
            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern IntPtr LoadImage(IntPtr hinst, IntPtr lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);
        }
    }
}
