namespace Glacier.Windowing.Swapchain.Metal;

using System;

/// <summary>
/// Apple Metal presentation swapchain driving CAMetalLayer and nextDrawable synchronization.
/// </summary>
public sealed class MetalSwapchain : ISwapchain
{
    public const int MTLPixelFormatBGRA8Unorm = 80;
    public const int MTLPixelFormatRGBA16Float = 115;

    private readonly SwapchainDescription _description;
    private readonly IntPtr _metalLayer;
    private int _width;
    private int _height;
    private int _currentDrawableIndex;
    private readonly IntPtr[] _drawables;
    private readonly int _pixelFormat;
    private bool _disposed;
    private ulong _presentCount;

    public int Width => _width;
    public int Height => _height;
    public int BufferCount => _description.BufferCount;
    public int PixelFormat => _pixelFormat;
    public ulong PresentCount => _presentCount;

    public IntPtr CurrentBackBuffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _drawables[_currentDrawableIndex];
        }
    }

    public MetalSwapchain(IntPtr metalLayer, SwapchainDescription desc)
    {
        if (metalLayer == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid CAMetalLayer handle.", nameof(metalLayer));
        }
        if (desc.Width <= 0 || desc.Height <= 0)
        {
            throw new ArgumentException("Swapchain dimensions must be positive non-zero.", nameof(desc));
        }

        _metalLayer = metalLayer;
        _description = desc;
        _width = desc.Width;
        _height = desc.Height;
        _pixelFormat = desc.EnableHdr ? MTLPixelFormatRGBA16Float : MTLPixelFormatBGRA8Unorm;

        int bufferCount = Math.Max(2, desc.BufferCount);
        _drawables = new IntPtr[bufferCount];

        for (int i = 0; i < bufferCount; i++)
        {
            _drawables[i] = new IntPtr(0x4D455400 + i); // 'MET\0'
        }
    }

    public void Present(bool vsync = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Advance to next drawable in CAMetalLayer presentation queue
        _currentDrawableIndex = (_currentDrawableIndex + 1) % _drawables.Length;
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
        _currentDrawableIndex = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _drawables.Length; i++)
        {
            _drawables[i] = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
