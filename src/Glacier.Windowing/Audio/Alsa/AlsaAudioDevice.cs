namespace Glacier.Windowing.Audio.Alsa;

using System;
using System.Runtime.InteropServices;

internal static unsafe partial class LibAsound
{
    private const string AsoundDll = "libasound.so.2";

    public const int SND_PCM_STREAM_PLAYBACK = 0;
    public const int SND_PCM_NONBLOCK = 0x00000001;
    public const int SND_PCM_FORMAT_FLOAT_LE = 14;
    public const int SND_PCM_ACCESS_RW_INTERLEAVED = 3;

    [DllImport(AsoundDll)]
    public static extern int snd_pcm_open(out IntPtr pcm, string name, int stream, int mode);

    [DllImport(AsoundDll)]
    public static extern int snd_pcm_close(IntPtr pcm);

    [DllImport(AsoundDll)]
    public static extern int snd_pcm_set_params(
        IntPtr pcm,
        int format,
        int access,
        uint channels,
        uint rate,
        int soft_resample,
        uint latency);

    [DllImport(AsoundDll)]
    public static extern IntPtr snd_pcm_writei(IntPtr pcm, void* buffer, UIntPtr size);
}

/// <summary>
/// Linux ALSA PCM audio output device interfacing with libasound.so.2.
/// </summary>
public sealed class AlsaAudioDevice : IAudioDevice
{
    private IntPtr _pcmHandle;
    private readonly int _sampleRate;
    private readonly int _channels;
    private float _volume = 1.0f;
    private bool _disposed;

    public int SampleRate => _sampleRate;
    public int Channels => _channels;

    public AlsaAudioDevice(int sampleRate = 48000, int channels = 2)
    {
        _sampleRate = sampleRate;
        _channels = channels;

        try
        {
            if (OperatingSystem.IsLinux())
            {
                int err = LibAsound.snd_pcm_open(out _pcmHandle, "default", LibAsound.SND_PCM_STREAM_PLAYBACK, LibAsound.SND_PCM_NONBLOCK);
                if (err == 0 && _pcmHandle != IntPtr.Zero)
                {
                    LibAsound.snd_pcm_set_params(_pcmHandle, LibAsound.SND_PCM_FORMAT_FLOAT_LE, LibAsound.SND_PCM_ACCESS_RW_INTERLEAVED, (uint)channels, (uint)sampleRate, 1, 10000);
                }
            }
        }
        catch
        {
            _pcmHandle = IntPtr.Zero;
        }
    }

    public void Play(ReadOnlySpan<float> pcmSamples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_pcmHandle == IntPtr.Zero || !OperatingSystem.IsLinux() || pcmSamples.IsEmpty)
        {
            return;
        }

        unsafe
        {
            fixed (float* ptr = pcmSamples)
            {
                UIntPtr frames = (UIntPtr)(pcmSamples.Length / _channels);
                LibAsound.snd_pcm_writei(_pcmHandle, ptr, frames);
            }
        }
    }

    public void SetMasterVolume(float volume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _volume = Math.Clamp(volume, 0.0f, 1.0f);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_pcmHandle != IntPtr.Zero && OperatingSystem.IsLinux())
        {
            try { LibAsound.snd_pcm_close(_pcmHandle); } catch { }
            _pcmHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
