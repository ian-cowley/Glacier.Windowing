namespace Glacier.Windowing.Benchmarks;

using System;
using System.Diagnostics;
using Glacier.Windowing.Audio;
using Glacier.Windowing.Platform.Headless;
using Glacier.Windowing.Swapchain;
using Glacier.Windowing.Swapchain.D3D12;
using Glacier.Windowing.Swapchain.Vulkan;

public static class DirectBenchmarkRunner
{
    public static void RunAll()
    {
        Console.WriteLine("================================================================================");
        Console.WriteLine("       GLACIER.WINDOWING (PILLAR 11) PERFORMANCE VERIFICATION SUITE             ");
        Console.WriteLine("================================================================================");

        // 1. Event Pump & Input Queue
        Console.WriteLine("\n[1] Lock-Free Event Pump Latency (1,000,000 events):");
        using var window = new HeadlessWindow(new WindowOptions());
        window.InputReceived += _ => { };
        var evt = new InputEvent(InputEventType.MouseMove, 0, 100, 200, 1000);

        // Warmup
        for (int w = 0; w < 100_000; w++)
        {
            window.EnqueueInput(evt);
            window.PollEvents();
        }

        const int iters = 1_000_000;
        var swPump = Stopwatch.StartNew();
        for (int i = 0; i < iters; i++)
        {
            window.EnqueueInput(evt);
            window.PollEvents();
        }
        swPump.Stop();
        double nsPerEvent = (swPump.Elapsed.TotalMilliseconds * 1_000_000.0) / iters;
        double throughput = iters / swPump.Elapsed.TotalSeconds;

        Console.WriteLine($"  - Enqueue + Poll Event: {nsPerEvent:F1} ns/event ({throughput / 1e6:F2} M events/sec)");

        // 2. Audio SPSC Ring Buffer
        Console.WriteLine("\n[2] Lock-Free SPSC Audio Ring Buffer Throughput (256-sample chunks):");
        var ringBuffer = new SpscAudioRingBuffer(16384);
        var samples256 = new float[256];
        var readBuffer = new float[256];
        for (int i = 0; i < 256; i++) samples256[i] = i * 0.01f;

        const int audioIters = 500_000;
        var swAudio = Stopwatch.StartNew();
        for (int i = 0; i < audioIters; i++)
        {
            ringBuffer.TryWrite(samples256);
            ringBuffer.Read(readBuffer);
        }
        swAudio.Stop();
        double totalSamples = (double)audioIters * 256;
        double samplesPerSec = totalSamples / swAudio.Elapsed.TotalSeconds;

        Console.WriteLine($"  - SPSC Write + Read:    {swAudio.Elapsed.TotalMilliseconds / audioIters * 1000.0:F1} ns/chunk ({samplesPerSec / 1e6:F2} M samples/sec)");

        // 3. Swapchain Present Latency
        Console.WriteLine("\n[3] Flip-Model & Mailbox Swapchain Presentation Latency (1920x1080):");
        var desc = new SwapchainDescription(1920, 1080, BufferCount: 3);
        using var d3d12 = new D3D12Swapchain(new IntPtr(0x1000), desc);
        using var vulkan = new VulkanSwapchain(new IntPtr(0x2000), desc);

        const int presentIters = 1_000_000;
        var swD3D = Stopwatch.StartNew();
        for (int i = 0; i < presentIters; i++)
        {
            d3d12.Present(vsync: false);
        }
        swD3D.Stop();
        double nsD3D = (swD3D.Elapsed.TotalMilliseconds * 1_000_000.0) / presentIters;

        var swVk = Stopwatch.StartNew();
        for (int i = 0; i < presentIters; i++)
        {
            vulkan.Present(vsync: false);
        }
        swVk.Stop();
        double nsVk = (swVk.Elapsed.TotalMilliseconds * 1_000_000.0) / presentIters;

        Console.WriteLine($"  - D3D12 Flip Present:     {nsD3D:F1} ns/call ({1_000_000_000.0 / nsD3D / 1e6:F2} M frames/sec)");
        Console.WriteLine($"  - Vulkan Mailbox Present: {nsVk:F1} ns/call ({1_000_000_000.0 / nsVk / 1e6:F2} M frames/sec)");

        Console.WriteLine("================================================================================");
        Console.WriteLine("           ALL WINDOWING BENCHMARKS COMPLETED SUCCESSFULLY                      ");
        Console.WriteLine("================================================================================");
    }
}
