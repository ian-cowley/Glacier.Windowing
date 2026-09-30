namespace Glacier.Windowing.Benchmarks;

using BenchmarkDotNet.Attributes;
using Glacier.Windowing.Audio;

[MemoryDiagnoser]
public class SpscAudioRingBufferBenchmarks
{
    private SpscAudioRingBuffer _ringBuffer = null!;
    private float[] _samples64 = null!;
    private float[] _samples256 = null!;
    private float[] _readBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ringBuffer = new SpscAudioRingBuffer(16384);
        _samples64 = new float[64];
        _samples256 = new float[256];
        _readBuffer = new float[256];

        for (int i = 0; i < 256; i++)
        {
            _samples256[i] = i * 0.01f;
        }
    }

    [Benchmark]
    public void WriteAndRead_64Samples()
    {
        _ringBuffer.TryWrite(_samples64);
        _ringBuffer.Read(_readBuffer.AsSpan(0, 64));
    }

    [Benchmark]
    public void WriteAndRead_256Samples()
    {
        _ringBuffer.TryWrite(_samples256);
        _ringBuffer.Read(_readBuffer);
    }
}
