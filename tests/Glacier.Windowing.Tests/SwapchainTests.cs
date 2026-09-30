namespace Glacier.Windowing.Tests;

using System;
using Glacier.Windowing.Platform.Headless;
using Glacier.Windowing.Swapchain;
using Glacier.Windowing.Swapchain.D3D12;
using Glacier.Windowing.Swapchain.Metal;
using Glacier.Windowing.Swapchain.Software;
using Glacier.Windowing.Swapchain.Vulkan;
using Xunit;

public class SwapchainTests
{
    [Fact]
    public void SwapchainDescription_DefaultsAndRecords_Work()
    {
        var desc = new SwapchainDescription(1920, 1080);
        Assert.Equal(1920, desc.Width);
        Assert.Equal(1080, desc.Height);
        Assert.Equal(2, desc.BufferCount);
        Assert.False(desc.EnableHdr);
        Assert.True(desc.LowLatencyWaitable);

        var custom = new SwapchainDescription(3840, 2160, BufferCount: 3, EnableHdr: true, LowLatencyWaitable: false);
        Assert.Equal(3840, custom.Width);
        Assert.Equal(2160, custom.Height);
        Assert.Equal(3, custom.BufferCount);
        Assert.True(custom.EnableHdr);
        Assert.False(custom.LowLatencyWaitable);
    }

    [Fact]
    public void D3D12Swapchain_BufferCyclingAndPresent_AdvancesFlipQueue()
    {
        var desc = new SwapchainDescription(1280, 720, BufferCount: 3);
        using var swapchain = new D3D12Swapchain(new IntPtr(0x1000), desc);

        Assert.Equal(1280, swapchain.Width);
        Assert.Equal(720, swapchain.Height);
        Assert.Equal(3, swapchain.BufferCount);
        Assert.Equal(0UL, swapchain.PresentCount);

        IntPtr b0 = swapchain.CurrentBackBuffer;
        Assert.NotEqual(IntPtr.Zero, b0);

        swapchain.Present(vsync: true);
        Assert.Equal(1UL, swapchain.PresentCount);
        IntPtr b1 = swapchain.CurrentBackBuffer;
        Assert.NotEqual(b0, b1);

        swapchain.Present(vsync: false);
        Assert.Equal(2UL, swapchain.PresentCount);
        IntPtr b2 = swapchain.CurrentBackBuffer;
        Assert.NotEqual(b1, b2);

        // Third present wraps around in 3-buffer queue
        swapchain.Present(vsync: true);
        Assert.Equal(3UL, swapchain.PresentCount);
        IntPtr b3 = swapchain.CurrentBackBuffer;
        Assert.Equal(b0, b3);

        // Resize
        swapchain.Resize(1920, 1080);
        Assert.Equal(1920, swapchain.Width);
        Assert.Equal(1080, swapchain.Height);
        Assert.Equal(b0, swapchain.CurrentBackBuffer);
    }

    [Fact]
    public void VulkanSwapchain_PresentModesAndSemaphores_FunctionCorrectly()
    {
        var desc = new SwapchainDescription(1920, 1080, BufferCount: 2);
        using var vk = new VulkanSwapchain(new IntPtr(0x2000), desc);

        Assert.Equal(1920, vk.Width);
        Assert.Equal(1080, vk.Height);
        Assert.Equal(2, vk.BufferCount);

        IntPtr img0 = vk.CurrentBackBuffer;
        IntPtr sem0 = vk.CurrentSemaphore;
        Assert.NotEqual(IntPtr.Zero, img0);
        Assert.NotEqual(IntPtr.Zero, sem0);

        vk.Present(vsync: false);
        Assert.Equal(VulkanInterop.VK_PRESENT_MODE_MAILBOX_KHR, vk.PresentMode);

        IntPtr img1 = vk.CurrentBackBuffer;
        Assert.NotEqual(img0, img1);

        vk.Present(vsync: true);
        Assert.Equal(VulkanInterop.VK_PRESENT_MODE_FIFO_KHR, vk.PresentMode);
        Assert.Equal(img0, vk.CurrentBackBuffer);
    }

    [Fact]
    public void MetalSwapchain_PixelFormatAndDrawables_AdvanceProperly()
    {
        var descSdr = new SwapchainDescription(800, 600, BufferCount: 2, EnableHdr: false);
        using var metalSdr = new MetalSwapchain(new IntPtr(0x3000), descSdr);
        Assert.Equal(MetalSwapchain.MTLPixelFormatBGRA8Unorm, metalSdr.PixelFormat);

        var descHdr = new SwapchainDescription(800, 600, BufferCount: 2, EnableHdr: true);
        using var metalHdr = new MetalSwapchain(new IntPtr(0x3000), descHdr);
        Assert.Equal(MetalSwapchain.MTLPixelFormatRGBA16Float, metalHdr.PixelFormat);

        IntPtr d0 = metalHdr.CurrentBackBuffer;
        metalHdr.Present();
        IntPtr d1 = metalHdr.CurrentBackBuffer;
        Assert.NotEqual(d0, d1);
    }

    [Fact]
    public void SoftwareSwapchain_AllocatesAndResizesBackbuffers()
    {
        var desc = new SwapchainDescription(100, 100, BufferCount: 2);
        using var sw = new SoftwareSwapchain(desc);

        Assert.Equal(100, sw.Width);
        Assert.Equal(100, sw.Height);
        Assert.Equal((nuint)(100 * 100 * 4), sw.BufferByteSize);

        IntPtr buf0 = sw.CurrentBackBuffer;
        Assert.NotEqual(IntPtr.Zero, buf0);

        sw.Present();
        IntPtr buf1 = sw.CurrentBackBuffer;
        Assert.NotEqual(buf0, buf1);

        sw.Resize(200, 150);
        Assert.Equal(200, sw.Width);
        Assert.Equal(150, sw.Height);
        Assert.Equal((nuint)(200 * 150 * 4), sw.BufferByteSize);
    }

    [Fact]
    public void SwapchainFactory_WithHeadlessWindow_ReturnsSoftwareSwapchain()
    {
        using var window = new HeadlessWindow(new WindowOptions());
        using var sc = SwapchainFactory.Create(window, new SwapchainDescription(640, 480));

        Assert.IsType<SoftwareSwapchain>(sc);
        Assert.Equal(640, sc.Width);
        Assert.Equal(480, sc.Height);
    }
}
