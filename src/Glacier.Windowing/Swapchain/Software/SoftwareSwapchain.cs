namespace Glacier.Windowing.Swapchain.Software;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// High-performance CPU software swapchain allocating unmanaged 32-bit RGBA backbuffers.
/// Used for headless testing, software rasterization fallback, and non-accelerated display targets.
/// </summary>
public sealed class SoftwareSwapchain : ISwapchain
{
    private readonly SwapchainDescription _description;
    private int _width;
    private int _height;
    private int _currentBufferIndex;
    private readonly IntPtr[] _buffers;
    private nuint _bufferByteSize;
    private bool _disposed;
    private ulong _presentCount;

    public int Width => _width;
    public int Height => _height;
    public int BufferCount => _description.BufferCount;
    public ulong PresentCount => _presentCount;
    public nuint BufferByteSize => _bufferByteSize;

    public IntPtr CurrentBackBuffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffers[_currentBufferIndex];
        }
    }

    public SoftwareSwapchain(SwapchainDescription desc)
    {
        if (desc.Width <= 0 || desc.Height <= 0)
        {
            throw new ArgumentException("Swapchain dimensions must be positive non-zero.", nameof(desc));
        }

        _description = desc;
        _width = desc.Width;
        _height = desc.Height;

        int bufferCount = Math.Max(2, desc.BufferCount);
        _buffers = new IntPtr[bufferCount];
        _bufferByteSize = (nuint)(_width * _height * 4);

        for (int i = 0; i < bufferCount; i++)
        {
            unsafe
            {
                _buffers[i] = (IntPtr)NativeMemory.AllocZeroed(_bufferByteSize);
            }
        }
    }

    public void Present(bool vsync = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Advance to next buffer in presentation queue
        _currentBufferIndex = (_currentBufferIndex + 1) % _buffers.Length;
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
        _bufferByteSize = (nuint)(_width * _height * 4);

        unsafe
        {
            for (int i = 0; i < _buffers.Length; i++)
            {
                if (_buffers[i] != IntPtr.Zero)
                {
                    NativeMemory.Free((void*)_buffers[i]);
                }
                _buffers[i] = (IntPtr)NativeMemory.AllocZeroed(_bufferByteSize);
            }
        }

        _currentBufferIndex = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        unsafe
        {
            for (int i = 0; i < _buffers.Length; i++)
            {
                if (_buffers[i] != IntPtr.Zero)
                {
                    NativeMemory.Free((void*)_buffers[i]);
                    _buffers[i] = IntPtr.Zero;
                }
            }
        }

        GC.SuppressFinalize(this);
    }
}
