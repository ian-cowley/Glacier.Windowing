namespace Glacier.Windowing.Platform.Win32;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class Dwmapi
{
    private const string DllName = "dwmapi.dll";

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    [DllImport(DllName, PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, void* pvAttribute, uint cbAttribute);
}

internal static unsafe partial class Kernel32
{
    private const string DllName = "kernel32.dll";

    [DllImport(DllName, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [DllImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    [DllImport(DllName)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryPerformanceFrequency(out long lpFrequency);
}
