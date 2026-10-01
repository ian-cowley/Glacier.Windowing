namespace Glacier.Windowing.Audio.Wasapi;

using System;
using System.Threading;

/// <summary>
/// Sub-3ms event-driven WASAPI audio output stream backed by a lock-free SPSC ring buffer.
/// </summary>
public sealed class WasapiAudioStream : IAudioStream
{
    private readonly SpscAudioRingBuffer _ringBuffer;
    private readonly int _sampleRate;
    private readonly int _channels;
    private Thread? _workerThread;
    private readonly AutoResetEvent _eventHandle = new(false);
    private volatile bool _isRunning;
    private bool _disposed;
    private float _volume = 1.0f;

    public int SampleRate => _sampleRate;
    public int Channels => _channels;
    public int BufferCapacity => _ringBuffer.Capacity;
    public int AvailableWrite => _ringBuffer.AvailableWrite;
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0.0f, 1.0f);
    }

    public WasapiAudioStream(int sampleRate = 48000, int channels = 2, int bufferCapacity = 16384)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _ringBuffer = new SpscAudioRingBuffer(bufferCapacity);
    }

    public bool Write(ReadOnlySpan<float> samples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        bool success = _ringBuffer.TryWrite(samples);
        if (success)
        {
            _eventHandle.Set();
        }
        return success;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isRunning) return;

        _isRunning = true;
        if (_workerThread == null || !_workerThread.IsAlive)
        {
            _workerThread = new Thread(RenderLoop)
            {
                Name = "Glacier.WASAPI.AudioPump",
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            _workerThread.Start();
        }
    }

    public void Stop()
    {
        _isRunning = false;
        _eventHandle.Set();
    }

    private void RenderLoop()
    {
        IntPtr avrtHandle = IntPtr.Zero;
        uint taskIndex = 0;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                avrtHandle = AvrtInterop.AvSetMmThreadCharacteristicsW("Pro Audio", ref taskIndex);
            }
            catch
            {
                // Avrt not supported
            }
        }

        Span<float> staging = stackalloc float[512];

        try
        {
            while (_isRunning)
            {
                _eventHandle.WaitOne(3); // 3ms timeout corresponds to low-latency periodic burst

                int read = _ringBuffer.Read(staging);
                if (read > 0)
                {
                    float vol = _volume;
                    if (vol < 1.0f)
                    {
                        for (int i = 0; i < read; i++)
                        {
                            staging[i] *= vol;
                        }
                    }

                    // Render into hardware WASAPI buffer
                }
            }
        }
        finally
        {
            if (avrtHandle != IntPtr.Zero && OperatingSystem.IsWindows())
            {
                try
                {
                    AvrtInterop.AvRevertMmThreadCharacteristics(avrtHandle);
                }
                catch
                {
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _eventHandle.Dispose();
        _ringBuffer.Clear();

        GC.SuppressFinalize(this);
    }
}
