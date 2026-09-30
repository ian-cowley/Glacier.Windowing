namespace Glacier.Windowing.Benchmarks;

using BenchmarkDotNet.Attributes;
using Glacier.Windowing.Platform.Headless;

[MemoryDiagnoser]
public class EventPumpBenchmarks
{
    private HeadlessWindow _window = null!;
    private readonly InputEvent _evt = new(InputEventType.MouseMove, 0, 100, 200, 1000);

    [GlobalSetup]
    public void Setup()
    {
        _window = new HeadlessWindow(new WindowOptions());
        _window.InputReceived += _ => { };
    }

    [Benchmark(Baseline = true)]
    public void PollEmptyQueue()
    {
        _window.PollEvents();
    }

    [Benchmark]
    public void EnqueueAndPollSingleEvent()
    {
        _window.EnqueueInput(_evt);
        _window.PollEvents();
    }

    [Benchmark]
    public void EnqueueAndPollTenEvents()
    {
        for (int i = 0; i < 10; i++)
        {
            _window.EnqueueInput(_evt);
        }
        _window.PollEvents();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _window.Dispose();
    }
}
