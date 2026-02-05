using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OpenClaw.Win.App;

public static class WindowThemeHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;
    private const int DWMWA_COLOR_DEFAULT = unchecked((int)0xFFFFFFFF);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void ApplyTitleBar(Window window, bool useDarkTheme)
    {
        if (window == null)
        {
            return;
        }

        if (!window.IsInitialized)
        {
            window.SourceInitialized += (_, _) => ApplyTitleBar(window, useDarkTheme);
            return;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var value = useDarkTheme ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));

        if (useDarkTheme)
        {
            var caption = ToColorRef(0x14, 0x1C, 0x26);
            var text = ToColorRef(0xE6, 0xED, 0xF3);
            var border = ToColorRef(0x23, 0x30, 0x42);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }
        else
        {
            var reset = DWMWA_COLOR_DEFAULT;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref reset, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref reset, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref reset, sizeof(int));
        }
    }

    private static int ToColorRef(byte r, byte g, byte b)
    {
        return r | (g << 8) | (b << 16);
    }
}
