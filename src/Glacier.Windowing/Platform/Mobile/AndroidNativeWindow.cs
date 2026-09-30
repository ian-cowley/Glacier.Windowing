namespace Glacier.Windowing.Platform.Mobile;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Glacier.Windowing.Swapchain;

/// <summary>
/// Android NDK HAL implementation interfacing directly with libandroid.so ANativeWindow.
/// </summary>
public sealed class AndroidNativeWindow : IWindow
{
    private const string AndroidDll = "libandroid.so";

    [DllImport(AndroidDll, SetLastError = true)]
    private static extern void ANativeWindow_acquire(IntPtr window);

    [DllImport(AndroidDll, SetLastError = true)]
    private static extern void ANativeWindow_release(IntPtr window);

    [DllImport(AndroidDll, SetLastError = true)]
    private static extern int ANativeWindow_getWidth(IntPtr window);

    [DllImport(AndroidDll, SetLastError = true)]
    private static extern int ANativeWindow_getHeight(IntPtr window);

    [DllImport(AndroidDll, SetLastError = true)]
    private static extern int ANativeWindow_setBuffersGeometry(IntPtr window, int width, int height, int format);

    private IntPtr _nativeWindow;
    private WindowSize _size;
    private string _title;
    private bool _isVisible;
    private bool _disposed;

    public IntPtr NativeHandle => _nativeWindow;

    public WindowSize Size
    {
        get => _size;
        set
        {
            _size = value;
            Resized?.Invoke(value.Width, value.Height);
        }
    }

    public string Title
    {
        get => _title;
        set => _title = value;
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => _isVisible = value;
    }

    public event Action<int, int>? Resized;
    public event Action<InputEvent>? InputReceived;
    public event Action? Closing;

    public AndroidNativeWindow(WindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);
        _isVisible = options.IsVisible;
        _nativeWindow = new IntPtr(0x414E4452); // 'ANDR' mock handle
    }

    /// <summary>
    /// Binds an active JNI surface or ANativeWindow pointer to this HAL window.
    /// </summary>
    public void AttachSurface(IntPtr nativeWindowHandle)
    {
        if (_nativeWindow != IntPtr.Zero && _nativeWindow != (IntPtr)0x414E4452 && OperatingSystem.IsAndroid())
        {
            try { ANativeWindow_release(_nativeWindow); } catch { }
        }

        _nativeWindow = nativeWindowHandle;
        if (_nativeWindow != IntPtr.Zero && OperatingSystem.IsAndroid())
        {
            try
            {
                ANativeWindow_acquire(_nativeWindow);
                int w = ANativeWindow_getWidth(_nativeWindow);
                int h = ANativeWindow_getHeight(_nativeWindow);
                if (w > 0 && h > 0)
                {
                    _size = new WindowSize(w, h);
                    Resized?.Invoke(w, h);
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// Enqueues batched touch motion snapshots directly into the input pipeline.
    /// </summary>
    public void DispatchTouch(ReadOnlySpan<TouchSnapshot> touches)
    {
        for (int i = 0; i < touches.Length; i++)
        {
            ref readonly TouchSnapshot t = ref touches[i];
            InputEventType type = t.Phase switch
            {
                TouchPhase.Began => InputEventType.TouchStart,
                TouchPhase.Moved => InputEventType.TouchMove,
                _ => InputEventType.TouchEnd
            };

            InputReceived?.Invoke(new InputEvent(type, t.PointerId, t.X, t.Y, t.TimestampNs));
        }
    }

    public void PollEvents()
    {
        // On Android, events are pumped via ALooper / NativeActivity
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

        Closing?.Invoke();
        if (_nativeWindow != IntPtr.Zero && _nativeWindow != (IntPtr)0x414E4452 && OperatingSystem.IsAndroid())
        {
            try { ANativeWindow_release(_nativeWindow); } catch { }
        }
        _nativeWindow = IntPtr.Zero;

        GC.SuppressFinalize(this);
    }
}
