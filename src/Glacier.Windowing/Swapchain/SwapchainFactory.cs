namespace Glacier.Windowing.Swapchain;

using System;
using Glacier.Windowing.Platform;

/// <summary>
/// Factory for selecting and constructing hardware swapchains matching the target platform and window.
/// </summary>
public static class SwapchainFactory
{
    /// <summary>
    /// Creates a hardware swapchain bound to the specified window.
    /// </summary>
    public static ISwapchain Create(IWindow window, SwapchainDescription desc, SwapchainBackend backend = SwapchainBackend.Auto)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window is Platform.Headless.HeadlessWindow || backend == SwapchainBackend.Software)
        {
            return new Software.SoftwareSwapchain(desc);
        }

        switch (backend)
        {
            case SwapchainBackend.Direct3D12:
                return new D3D12.D3D12Swapchain(window.NativeHandle, desc);

            case SwapchainBackend.Vulkan:
                return new Vulkan.VulkanSwapchain(window.NativeHandle, desc);

            case SwapchainBackend.Metal:
                return new Metal.MetalSwapchain(window.NativeHandle, desc);

            case SwapchainBackend.Auto:
            default:
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        return new D3D12.D3D12Swapchain(window.NativeHandle, desc);
                    }
                    catch
                    {
                        return new Vulkan.VulkanSwapchain(window.NativeHandle, desc);
                    }
                }
                if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
                {
                    return new Metal.MetalSwapchain(window.NativeHandle, desc);
                }
                if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
                {
                    return new Vulkan.VulkanSwapchain(window.NativeHandle, desc);
                }
                return new Software.SoftwareSwapchain(desc);
        }
    }
}
