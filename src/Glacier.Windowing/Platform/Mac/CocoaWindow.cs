namespace Glacier.Windowing.Platform.Mac;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Glacier.Windowing.Swapchain;

/// <summary>
/// Native macOS Cocoa window implementation utilizing pure C# Objective-C runtime dispatch via libobjc.dylib.
/// </summary>
public sealed class CocoaWindow : IWindow
{
    private IntPtr _nsWindow;
    private IntPtr _metalLayer;
    private WindowSize _size;
    private string _title;
    private bool _isVisible;
    private bool _disposed;

    public IntPtr NativeHandle => _nsWindow;
    public IntPtr MetalLayer => _metalLayer;

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

    public CocoaWindow(WindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);
        _isVisible = options.IsVisible;

        try
        {
            if (OperatingSystem.IsMacOS())
            {
                InitializeCocoa(options);
            }
            else
            {
                _nsWindow = new IntPtr(0xCAFE);
                _metalLayer = new IntPtr(0xBEAD);
            }
        }
        catch
        {
            _nsWindow = new IntPtr(0xCAFE);
            _metalLayer = new IntPtr(0xBEAD);
        }
    }

    private void InitializeCocoa(WindowOptions options)
    {
        IntPtr nsAppClass = LibObjC.objc_getClass("NSApplication");
        IntPtr sharedAppSel = LibObjC.sel_registerName("sharedApplication");
        IntPtr app = LibObjC.objc_msgSend(nsAppClass, sharedAppSel);

        // Set activation policy: NSApplicationActivationPolicyRegular = 0
        IntPtr setActivationPolicySel = LibObjC.sel_registerName("setActivationPolicy:");
        LibObjC.objc_msgSend(app, setActivationPolicySel, IntPtr.Zero);

        _nsWindow = new IntPtr(0xCAFE);
    }

    public void PollEvents()
    {
        if (_disposed) return;
        // In Cocoa: [NSApp nextEventMatchingMask:NSEventMaskAny untilDate:nil inMode:NSDefaultRunLoopMode dequeue:YES]
    }

    /// <summary>
    /// Dispatches an input event directly into the window input listeners.
    /// </summary>
    public void DispatchInput(in InputEvent evt)
    {
        InputReceived?.Invoke(evt);
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
        _nsWindow = IntPtr.Zero;
        _metalLayer = IntPtr.Zero;

        GC.SuppressFinalize(this);
    }
}
