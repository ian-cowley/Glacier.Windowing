namespace Glacier.Windowing.Platform.Headless;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Glacier.Windowing.Swapchain;

/// <summary>
/// Fully functional virtual window implementation for headless servers, background renderers, and automated unit tests.
/// </summary>
public sealed class HeadlessWindow : IWindow
{
    private readonly ConcurrentQueue<InputEvent> _eventQueue = new();
    private WindowSize _size;
    private string _title;
    private bool _isVisible;
    private bool _disposed;

    public IntPtr NativeHandle { get; }

    public WindowSize Size
    {
        get => _size;
        set
        {
            if (_size == value) return;
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

    public HeadlessWindow(WindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);
        _isVisible = options.IsVisible;
        NativeHandle = new IntPtr(0xDEADBEEF);
    }

    /// <summary>
    /// Injects a synthetic input event into the virtual message queue.
    /// </summary>
    public void EnqueueInput(in InputEvent evt)
    {
        _eventQueue.Enqueue(evt);
    }

    public void PollEvents()
    {
        if (_disposed) return;

        while (_eventQueue.TryDequeue(out InputEvent evt))
        {
            InputReceived?.Invoke(evt);
        }
    }

    public ISwapchain CreateSwapchain(SwapchainDescription desc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SwapchainFactory.Create(this, desc);
    }

    public void TriggerClosing()
    {
        Closing?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Closing?.Invoke();
        _eventQueue.Clear();

        GC.SuppressFinalize(this);
    }
}
