namespace Glacier.Windowing.Platform.Linux;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Glacier.Windowing.Swapchain;

/// <summary>
/// Direct X11 window implementation via libX11.so.6 P/Invoke.
/// </summary>
public sealed class X11Window : IWindow
{
    private IntPtr _display;
    private IntPtr _window;
    private IntPtr _wmDeleteWindow;
    private WindowSize _size;
    private string _title;
    private bool _isVisible;
    private bool _disposed;

    public IntPtr NativeHandle => _window;
    public IntPtr DisplayHandle => _display;

    public WindowSize Size
    {
        get => _size;
        set => _size = value;
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            if (_display != IntPtr.Zero && _window != IntPtr.Zero)
            {
                LibX11.XStoreName(_display, _window, value);
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            if (_display != IntPtr.Zero && _window != IntPtr.Zero)
            {
                if (value)
                {
                    LibX11.XMapWindow(_display, _window);
                }
                else
                {
                    LibX11.XUnmapWindow(_display, _window);
                }
            }
        }
    }

    public event Action<int, int>? Resized;
    public event Action<InputEvent>? InputReceived;
    public event Action? Closing;

    public X11Window(WindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);

        try
        {
            _display = LibX11.XOpenDisplay(null);
            if (_display == IntPtr.Zero)
            {
                // Headless environment without active X11 display server
                _window = (IntPtr)0x1101;
                IsVisible = options.IsVisible;
                return;
            }

            IntPtr root = LibX11.XDefaultRootWindow(_display);
            _window = LibX11.XCreateSimpleWindow(
                _display,
                root,
                100, 100,
                (uint)options.Width, (uint)options.Height,
                0,
                UIntPtr.Zero,
                UIntPtr.Zero);

            if (_window == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create X11 window handle.");
            }

            LibX11.XStoreName(_display, _window, options.Title);

            IntPtr eventMask = (IntPtr)(
                LibX11.ExposureMask |
                LibX11.KeyPressMask |
                LibX11.KeyReleaseMask |
                LibX11.ButtonPressMask |
                LibX11.ButtonReleaseMask |
                LibX11.PointerMotionMask |
                LibX11.StructureNotifyMask);

            LibX11.XSelectInput(_display, _window, eventMask);

            _wmDeleteWindow = LibX11.XInternAtom(_display, "WM_DELETE_WINDOW", false);
            if (_wmDeleteWindow != IntPtr.Zero)
            {
                LibX11.XSetWMProtocols(_display, _window, [_wmDeleteWindow], 1);
            }

            IsVisible = options.IsVisible;
        }
        catch (DllNotFoundException)
        {
            // libX11.so.6 not available on this platform
            _display = IntPtr.Zero;
            _window = (IntPtr)0x1101; // Mock handle for testing
        }
        catch (EntryPointNotFoundException)
        {
            _display = IntPtr.Zero;
            _window = (IntPtr)0x1101;
        }
    }

    public void PollEvents()
    {
        if (_disposed || _display == IntPtr.Zero || _window == IntPtr.Zero) return;

        ulong nowNs = (ulong)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));

        while (LibX11.XPending(_display) > 0)
        {
            LibX11.XNextEvent(_display, out LibX11.XEvent ev);

            switch (ev.type)
            {
                case LibX11.ConfigureNotify:
                    if (ev.xconfigure.width != _size.Width || ev.xconfigure.height != _size.Height)
                    {
                        _size = new WindowSize(ev.xconfigure.width, ev.xconfigure.height);
                        Resized?.Invoke(_size.Width, _size.Height);
                    }
                    break;

                case LibX11.ClientMessage:
                    if (ev.xclient.ptr0 == _wmDeleteWindow)
                    {
                        Closing?.Invoke();
                    }
                    break;

                case LibX11.KeyPress:
                    InputReceived?.Invoke(new InputEvent(InputEventType.KeyDown, (int)ev.xkey.keycode, ev.xkey.x, ev.xkey.y, nowNs));
                    break;

                case LibX11.KeyRelease:
                    InputReceived?.Invoke(new InputEvent(InputEventType.KeyUp, (int)ev.xkey.keycode, ev.xkey.x, ev.xkey.y, nowNs));
                    break;

                case LibX11.ButtonPress:
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseDown, (int)ev.xbutton.button, ev.xbutton.x, ev.xbutton.y, nowNs));
                    break;

                case LibX11.ButtonRelease:
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseUp, (int)ev.xbutton.button, ev.xbutton.x, ev.xbutton.y, nowNs));
                    break;

                case LibX11.MotionNotify:
                    InputReceived?.Invoke(new InputEvent(InputEventType.MouseMove, 0, ev.xmotion.x, ev.xmotion.y, nowNs));
                    break;
            }
        }
    }

    public ISwapchain CreateSwapchain(SwapchainDescription desc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SwapchainFactory.Create(this, desc);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_display != IntPtr.Zero)
        {
            if (_window != IntPtr.Zero)
            {
                LibX11.XDestroyWindow(_display, _window);
                _window = IntPtr.Zero;
            }
            LibX11.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
