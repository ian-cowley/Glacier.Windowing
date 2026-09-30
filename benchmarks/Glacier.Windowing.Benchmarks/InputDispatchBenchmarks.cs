namespace Glacier.Windowing.Benchmarks;

using BenchmarkDotNet.Attributes;
using Glacier.Windowing.Input;

[MemoryDiagnoser]
public class InputDispatchBenchmarks
{
    private InputState _inputState = null!;
    private readonly InputEvent _mouseMoveEvt = new(InputEventType.MouseMove, 0, 1024, 768, 1000);
    private readonly InputEvent _keyDownEvt = new(InputEventType.KeyDown, 65, 0, 0, 2000);
    private readonly InputEvent _keyUpEvt = new(InputEventType.KeyUp, 65, 0, 0, 3000);

    [GlobalSetup]
    public void Setup()
    {
        _inputState = new InputState();
    }

    [Benchmark]
    public void DispatchMouseMove()
    {
        _inputState.OnInput(in _mouseMoveEvt);
    }

    [Benchmark]
    public void DispatchKeyStroke()
    {
        _inputState.OnInput(in _keyDownEvt);
        _inputState.OnInput(in _keyUpEvt);
    }
}
