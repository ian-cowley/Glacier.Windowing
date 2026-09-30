namespace Glacier.Windowing.Platform.Win32;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Glacier.Windowing.Swapchain;

/// <summary>
/// Native high-performance Win32 window implementation utilizing direct P/Invoke to User32 and Dwmapi.
/// </summary>
public sealed unsafe class Win32Window : IWindow
{
    private static bool s_dpiAwarenessInitialized;
    private readonly string _className;
    private readonly User32.WndProc _wndProcDelegate;
    private IntPtr _hInstance;
    private IntPtr _hWnd;
    private bool _disposed;
    private bool _isVisible;
    private WindowSize _size;
    private string _title;
    private readonly bool _rawInputEnabled;

    public IntPtr NativeHandle => _hWnd;

    public WindowSize Size
    {
        get => _size;
        set
        {
            if (_size == value) return;
            _size = value;
            if (_hWnd != IntPtr.Zero)
            {
                User32.RECT rect = new() { left = 0, top = 0, right = value.Width, bottom = value.Height };
                User32.AdjustWindowRectEx(ref rect, User32.WS_OVERLAPPEDWINDOW, false, 0);
                User32.SetWindowPos(_hWnd, IntPtr.Zero, 0, 0, rect.Width, rect.Height, 0x0002 | 0x0004); // SWP_NOMOVE | SWP_NOZORDER
            }
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            if (_hWnd != IntPtr.Zero)
            {
                User32.SetWindowTextW(_hWnd, value);
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            if (_hWnd != IntPtr.Zero)
            {
                User32.ShowWindow(_hWnd, value ? User32.SW_SHOWNORMAL : User32.SW_HIDE);
            }
        }
    }

    public event Action<int, int>? Resized;
    public event Action<InputEvent>? InputReceived;
    public event Action? Closing;

    public Win32Window(WindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);
        _rawInputEnabled = options.EnableRawInput;

        InitializeDpiAwareness(options.PerMonitorDpiAware);

        _className = $"Glacier_Win32_{Guid.NewGuid():N}";
        _hInstance = Kernel32.GetModuleHandleW(null);
        _wndProcDelegate = ProcessMessage;

        User32.WNDCLASSEXW wc = new()
        {
            cbSize = (uint)Marshal.SizeOf<User32.WNDCLASSEXW>(),
            style = 0x0020 | 0x0002 | 0x0001, // CS_OWNDC | CS_HREDRAW | CS_VREDRAW
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = _hInstance,
            lpszClassName = _className,
            hCursor = User32.LoadCursorW(IntPtr.Zero, (IntPtr)32512) // IDC_ARROW
        };

        if (User32.RegisterClassExW(ref wc) == 0)
        {
            int err = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to register Win32 window class '{_className}'. Error code: {err}");
        }

        uint style = User32.WS_OVERLAPPEDWINDOW;
        if (!options.Resizable)
        {
            style &= ~((uint)User32.WS_THICKFRAME | (uint)User32.WS_MAXIMIZEBOX);
        }

        User32.RECT rect = new() { left = 0, top = 0, right = options.Width, bottom = options.Height };
        User32.AdjustWindowRectEx(ref rect, style, false, User32.WS_EX_APPWINDOW);

        _hWnd = User32.CreateWindowExW(
            User32.WS_EX_APPWINDOW,
            _className,
            options.Title,
            style,
            100,
            100,
            rect.Width,
            rect.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            _hInstance,
            IntPtr.Zero);

        if (_hWnd == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            User32.UnregisterClassW(_className, _hInstance);
            throw new InvalidOperationException($"Failed to create native Win32 window. Error code: {err}");
        }

        if (options.DarkMode)
        {
            int useDarkMode = 1;
            Dwmapi.DwmSetWindowAttribute(_hWnd, Dwmapi.DWMWA_USE_IMMERSIVE_DARK_MODE, &useDarkMode, sizeof(int));
        }

        if (options.EnableRawInput)
        {
            Win32RawInput.Register(_hWnd);
        }

        IsVisible = options.IsVisible;
    }

    private static void InitializeDpiAwareness(bool perMonitorDpi)
    {
        if (s_dpiAwarenessInitialized || !perMonitorDpi) return;
        try
        {
            User32.SetProcessDpiAwarenessContext(User32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch
        {
            // Fallback gracefully on older OS revisions
        }
        s_dpiAwarenessInitialized = true;
    }

    public void PollEvents()
    {
        if (_disposed || _hWnd == IntPtr.Zero) return;

        while (User32.PeekMessageW(out User32.MSG msg, _hWnd, 0, 0, User32.PM_REMOVE))
        {
            User32.TranslateMessage(ref msg);
            User32.DispatchMessageW(ref msg);
        }
    }

    public ISwapchain CreateSwapchain(SwapchainDescription desc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SwapchainFactory.Create(this, desc);
    }

    private IntPtr ProcessMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        ulong nowNs = (ulong)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));

        switch (msg)
        {
            case User32.WM_SIZE:
                int width = (int)(lParam.ToInt64() & 0xFFFF);
                int height = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
                _size = new WindowSize(width, height);
                Resized?.Invoke(width, height);
                return IntPtr.Zero;

            case User32.WM_CLOSE:
                Closing?.Invoke();
                User32.DestroyWindow(hWnd);
                return IntPtr.Zero;

            case User32.WM_DESTROY:
                _hWnd = IntPtr.Zero;
                return IntPtr.Zero;

            case User32.WM_INPUT:
                if (_rawInputEnabled)
                {
                    Win32RawInput.Process(lParam, InputReceived, nowNs);
                }
                break;

            case User32.WM_KEYDOWN:
                if (!_rawInputEnabled)
                {
                    InputReceived?.Invoke(new InputEvent(InputEventType.KeyDown, (int)wParam, 0, 0, nowNs));
                }
                break;

            case User32.WM_KEYUP:
                if (!_rawInputEnabled)
                {
                    InputReceived?.Invoke(new InputEvent(InputEventType.KeyUp, (int)wParam, 0, 0, nowNs));
                }
                break;

            case User32.WM_MOUSEMOVE:
                if (!_rawInputEnabled)
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseMove, 0, mx, my, nowNs));
                }
                break;

            case User32.WM_LBUTTONDOWN:
                if (!_rawInputEnabled)
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseDown, 0, mx, my, nowNs));
                }
                break;

            case User32.WM_LBUTTONUP:
                if (!_rawInputEnabled)
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseUp, 0, mx, my, nowNs));
                }
                break;

            case User32.WM_RBUTTONDOWN:
                if (!_rawInputEnabled)
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseDown, 1, mx, my, nowNs));
                }
                break;

            case User32.WM_RBUTTONUP:
                if (!_rawInputEnabled)
                {
                    int mx = (short)(lParam.ToInt64() & 0xFFFF);
                    int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseUp, 1, mx, my, nowNs));
                }
                break;

            case User32.WM_MOUSEWHEEL:
                if (!_rawInputEnabled)
                {
                    short delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseWheel, 0, 0, delta, nowNs));
                }
                break;
        }

        return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hWnd != IntPtr.Zero)
        {
            User32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        if (!string.IsNullOrEmpty(_className) && _hInstance != IntPtr.Zero)
        {
            User32.UnregisterClassW(_className, _hInstance);
        }

        GC.SuppressFinalize(this);
    }

    ~Win32Window()
    {
        Dispose();
    }
}
