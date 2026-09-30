namespace Glacier.Windowing.Benchmarks;

using BenchmarkDotNet.Attributes;
using Glacier.Windowing.Swapchain;
using Glacier.Windowing.Swapchain.D3D12;
using Glacier.Windowing.Swapchain.Vulkan;

[MemoryDiagnoser]
public class SwapchainBenchmarks
{
    private D3D12Swapchain _d3d12 = null!;
    private VulkanSwapchain _vulkan = null!;

    [GlobalSetup]
    public void Setup()
    {
        var desc = new SwapchainDescription(1920, 1080, BufferCount: 3);
        _d3d12 = new D3D12Swapchain(new IntPtr(0x1000), desc);
        _vulkan = new VulkanSwapchain(new IntPtr(0x2000), desc);
    }

    [Benchmark(Baseline = true)]
    public void D3D12FlipPresent()
    {
        _d3d12.Present(vsync: false);
    }

    [Benchmark]
    public void VulkanMailboxPresent()
    {
        _vulkan.Present(vsync: false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _d3d12.Dispose();
        _vulkan.Dispose();
    }
}
