using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace PCSoundBooster;

internal static class TrayPlacement
{
    private const int ExtendedStyle = -20;
    private const int ToolWindow = 0x80;
    private const int AppWindow = 0x40000;
    private const int KeepSizeOrderAndActivation = 0x0015;

    internal static Point DefaultAnchor()
    {
        var screen = Forms.Screen.PrimaryScreen!;
        return new Point(screen.WorkingArea.Right - 28, screen.Bounds.Bottom - 2);
    }

    internal static void HideFromTaskSwitcher(Window panel)
    {
        var handle = new WindowInteropHelper(panel).Handle;
        SetWindowLong(handle, ExtendedStyle, (GetWindowLong(handle, ExtendedStyle) | ToolWindow) & ~AppWindow);
    }

    internal static void Position(Window panel, Point anchor)
    {
        var handle = new WindowInteropHelper(panel).Handle;
        if (!GetWindowRect(handle, out var rect))
            return;

        var screen = Forms.Screen.FromPoint(anchor);
        var origin = GetOrigin(screen.Bounds, screen.WorkingArea, anchor,
            new Size(rect.Right - rect.Left, rect.Bottom - rect.Top));
        SetWindowPos(handle, IntPtr.Zero, origin.X, origin.Y, 0, 0, KeepSizeOrderAndActivation);
    }

    internal static Point GetOrigin(Rectangle screen, Rectangle work, Point anchor, Size size)
    {
        var reserved = Math.Max(Math.Max(work.Left - screen.Left, screen.Right - work.Right),
            Math.Max(work.Top - screen.Top, screen.Bottom - work.Bottom));
        var nearest = Math.Min(Math.Min(anchor.X - screen.Left, screen.Right - anchor.X),
            Math.Min(anchor.Y - screen.Top, screen.Bottom - anchor.Y));
        var x = anchor.X - size.Width / 2;
        var y = anchor.Y - size.Height / 2;

        if (reserved > 0 ? screen.Bottom - work.Bottom == reserved : screen.Bottom - anchor.Y == nearest)
            y = work.Bottom - size.Height;
        else if (reserved > 0 ? work.Top - screen.Top == reserved : anchor.Y - screen.Top == nearest)
            y = work.Top;
        else if (reserved > 0 ? screen.Right - work.Right == reserved : screen.Right - anchor.X == nearest)
            x = work.Right - size.Width;
        else
            x = work.Left;

        return new Point(Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - size.Width)),
            Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - size.Height)));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        internal int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out WindowRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y,
        int width, int height, int flags);
}
