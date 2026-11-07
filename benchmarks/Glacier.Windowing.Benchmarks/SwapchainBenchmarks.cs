namespace Glacier.Windowing.Benchmarks;

using BenchmarkDotNet.Attributes;
using Glacier.Windowing.Platform;
using Glacier.Windowing.Swapchain;
using Glacier.Windowing.Swapchain.Software;

[MemoryDiagnoser]
public class SwapchainBenchmarks
{
    private IWindow _window = null!;
    private ISwapchain _primarySwapchain = null!;
    private SoftwareSwapchain _softwareSwapchain = null!;

    [GlobalSetup]
    public void Setup()
    {
        var desc = new SwapchainDescription(1920, 1080, BufferCount: 3);
        _window = WindowFactory.CreateWindow(new WindowOptions { Width = 1920, Height = 1080, IsVisible = false });
        _primarySwapchain = SwapchainFactory.Create(_window, desc);
        _softwareSwapchain = new SoftwareSwapchain(desc);
    }

    [Benchmark(Baseline = true)]
    public void PlatformAutoPresent()
    {
        _primarySwapchain.Present(vsync: false);
    }

    [Benchmark]
    public void SoftwareSwapchainPresent()
    {
        _softwareSwapchain.Present(vsync: false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _softwareSwapchain.Dispose();
        _primarySwapchain.Dispose();
        _window.Dispose();
    }
}
