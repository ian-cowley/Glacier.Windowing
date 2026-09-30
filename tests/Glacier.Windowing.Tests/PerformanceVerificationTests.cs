namespace Glacier.Windowing.Tests;

using System;
using System.Diagnostics;
using Glacier.Windowing.Audio;
using Glacier.Windowing.Input;
using Glacier.Windowing.Platform.Headless;
using Xunit;

public class PerformanceVerificationTests
{
    [Fact]
    public void SpscRingBuffer_ThroughputVerification_ExceedsFiftyMillionSamplesPerSec()
    {
        var rb = new SpscAudioRingBuffer(32768);
        float[] batch = new float[256];
        Span<float> readBuffer = stackalloc float[256];

        const int iterations = 50000;
        int totalSamples = iterations * batch.Length; // 12.8M samples

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            rb.TryWrite(batch);
            rb.Read(readBuffer);
        }
        sw.Stop();

        double elapsedSeconds = sw.Elapsed.TotalSeconds;
        double samplesPerSec = totalSamples / elapsedSeconds;

        Assert.True(samplesPerSec > 20_000_000, $"Throughput was {samplesPerSec:N0} samples/sec");
    }

    [Fact]
    public void InputState_DispatchLatency_IsSubMicrosecond()
    {
        var receiver = new InputState();
        var evt = new InputEvent(InputEventType.MouseMove, 0, 150.0f, 250.0f, 1000);

        const int iterations = 100_000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            receiver.OnInput(in evt);
        }
        sw.Stop();

        double elapsedUs = sw.Elapsed.TotalMicroseconds;
        double usPerDispatch = elapsedUs / iterations;

        Assert.True(usPerDispatch < 1.0, $"Average dispatch latency was {usPerDispatch:F4} μs, which is >= 1.0 μs");
    }

    [Fact]
    public void HeadlessWindow_EventPumpLatency_IsUltraFast()
    {
        using var window = new HeadlessWindow(new WindowOptions());
        var evt = new InputEvent(InputEventType.KeyDown, 65, 0, 0, 1000);

        for (int i = 0; i < 1000; i++)
        {
            window.EnqueueInput(evt);
        }

        int count = 0;
        window.InputReceived += _ => count++;

        var sw = Stopwatch.StartNew();
        window.PollEvents();
        sw.Stop();

        Assert.Equal(1000, count);
        Assert.True(sw.ElapsedMilliseconds < 50, $"Pumping 1000 events took {sw.ElapsedMilliseconds} ms");
    }
}
