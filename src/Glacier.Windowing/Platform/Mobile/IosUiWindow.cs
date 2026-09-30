namespace Glacier.Windowing.Platform.Mobile;

using System;
using System.Diagnostics;
using Glacier.Windowing.Swapchain;

/// <summary>
/// iOS UIKit Surface HAL implementation managing UIWindow, CAMetalLayer, and 120Hz ProMotion display link.
/// </summary>
public sealed class IosUiWindow : IWindow
{
    private IntPtr _uiWindow;
    private IntPtr _metalLayer;
    private WindowSize _size;
    private string _title;
    private bool _isVisible;
    private bool _disposed;
    private double _displayRefreshRate = 120.0;

    public IntPtr NativeHandle => _uiWindow;
    public IntPtr MetalLayer => _metalLayer;
    public double DisplayRefreshRate => _displayRefreshRate;

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

    public IosUiWindow(WindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);
        _isVisible = options.IsVisible;
        _uiWindow = new IntPtr(0x105001); // Mock handle
        _metalLayer = new IntPtr(0x105002);
    }

    /// <summary>
    /// Updates the display link refresh rate (e.g. 60Hz or 120Hz ProMotion).
    /// </summary>
    public void SetPreferredFramesPerSecond(int fps)
    {
        _displayRefreshRate = fps;
    }

    /// <summary>
    /// Dispatches batched touch snapshots with zero heap allocations.
    /// </summary>
    public void DispatchTouches(ReadOnlySpan<TouchSnapshot> touches)
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
        // On iOS, events are pumped via NSRunLoop / CADisplayLink
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
        _uiWindow = IntPtr.Zero;
        _metalLayer = IntPtr.Zero;

        GC.SuppressFinalize(this);
    }
}
