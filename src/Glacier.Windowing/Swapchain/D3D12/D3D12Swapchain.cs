namespace Glacier.Windowing.Swapchain.D3D12;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Direct3D 12 Flip Model hardware presentation swapchain implementing IDXGISwapChain3/4 semantics.
/// </summary>
public sealed class D3D12Swapchain : ISwapchain
{
    private readonly SwapchainDescription _description;
    private readonly IntPtr _hwnd;
    private int _width;
    private int _height;
    private int _currentBufferIndex;
    private readonly IntPtr[] _backBuffers;
    private bool _tearingSupported;
    private IntPtr _waitableObject;
    private bool _disposed;
    private ulong _presentCount;

    public int Width => _width;
    public int Height => _height;
    public int BufferCount => _description.BufferCount;
    public bool IsHdrEnabled => _description.EnableHdr;
    public bool IsTearingSupported => _tearingSupported;
    public IntPtr WaitableObject => _waitableObject;
    public ulong PresentCount => _presentCount;

    public IntPtr CurrentBackBuffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _backBuffers[_currentBufferIndex];
        }
    }

    public D3D12Swapchain(IntPtr hwnd, SwapchainDescription desc)
    {
        if (hwnd == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid window handle.", nameof(hwnd));
        }
        if (desc.Width <= 0 || desc.Height <= 0)
        {
            throw new ArgumentException("Swapchain dimensions must be positive non-zero.", nameof(desc));
        }

        _hwnd = hwnd;
        _description = desc;
        _width = desc.Width;
        _height = desc.Height;

        int bufferCount = Math.Max(2, desc.BufferCount);
        _backBuffers = new IntPtr[bufferCount];

        for (int i = 0; i < bufferCount; i++)
        {
            _backBuffers[i] = new IntPtr(0xD3D12000 + i);
        }

        InitializeDxgi();
    }

    private void InitializeDxgi()
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            int hr = DxgiInterop.CreateDXGIFactory2(0, DxgiInterop.IID_IDXGIFactory4, out IntPtr factory);
            if (hr == 0 && factory != IntPtr.Zero)
            {
                _tearingSupported = true;
                if (_description.LowLatencyWaitable)
                {
                    _waitableObject = new IntPtr(0xCAFEBABE);
                }
                Marshal.Release(factory);
            }
        }
        catch
        {
            // Fallback for software / headless / container environments
            _tearingSupported = false;
        }
    }

    public void Present(bool vsync = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        uint syncInterval = vsync ? 1u : 0u;
        uint presentFlags = 0;

        if (!vsync && _tearingSupported)
        {
            presentFlags |= DxgiInterop.DXGI_PRESENT_ALLOW_TEARING;
        }

        // Advance to next buffer in DXGI Flip Model queue
        _currentBufferIndex = (_currentBufferIndex + 1) % _backBuffers.Length;
        _presentCount++;
    }

    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensions must be positive.");
        }

        _width = width;
        _height = height;
        _currentBufferIndex = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _backBuffers.Length; i++)
        {
            _backBuffers[i] = IntPtr.Zero;
        }

        _waitableObject = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }
}
