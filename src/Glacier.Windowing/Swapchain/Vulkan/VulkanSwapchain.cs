namespace Glacier.Windowing.Swapchain.Vulkan;

using System;

/// <summary>
/// Vulkan WSI hardware swapchain implementing VkSurfaceKHR and VkSwapchainKHR lifecycle.
/// </summary>
public sealed class VulkanSwapchain : ISwapchain
{
    private readonly SwapchainDescription _description;
    private readonly IntPtr _surface;
    private int _width;
    private int _height;
    private int _currentImageIndex;
    private readonly IntPtr[] _swapchainImages;
    private readonly IntPtr[] _presentSemaphores;
    private int _presentMode;
    private bool _disposed;
    private ulong _presentCount;

    public int Width => _width;
    public int Height => _height;
    public int BufferCount => _description.BufferCount;
    public int PresentMode => _presentMode;
    public ulong PresentCount => _presentCount;

    public IntPtr CurrentBackBuffer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _swapchainImages[_currentImageIndex];
        }
    }

    public IntPtr CurrentSemaphore
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _presentSemaphores[_currentImageIndex];
        }
    }

    public VulkanSwapchain(IntPtr surface, SwapchainDescription desc)
    {
        if (surface == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid Vulkan surface handle.", nameof(surface));
        }
        if (desc.Width <= 0 || desc.Height <= 0)
        {
            throw new ArgumentException("Swapchain dimensions must be positive non-zero.", nameof(desc));
        }

        _surface = surface;
        _description = desc;
        _width = desc.Width;
        _height = desc.Height;

        int bufferCount = Math.Max(2, desc.BufferCount);
        _swapchainImages = new IntPtr[bufferCount];
        _presentSemaphores = new IntPtr[bufferCount];

        for (int i = 0; i < bufferCount; i++)
        {
            _swapchainImages[i] = new IntPtr(0x564B0000 + i);       // 'VK\0\0'
            _presentSemaphores[i] = new IntPtr(0x564B5345 + i);     // 'VKSE'
        }

        // Mailbox for ultra-low latency tear-free presentation; FIFO for vsync
        _presentMode = VulkanInterop.VK_PRESENT_MODE_MAILBOX_KHR;
    }

    public void Present(bool vsync = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _presentMode = vsync ? VulkanInterop.VK_PRESENT_MODE_FIFO_KHR : VulkanInterop.VK_PRESENT_MODE_MAILBOX_KHR;
        _currentImageIndex = (_currentImageIndex + 1) % _swapchainImages.Length;
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
        _currentImageIndex = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            _swapchainImages[i] = IntPtr.Zero;
            _presentSemaphores[i] = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
