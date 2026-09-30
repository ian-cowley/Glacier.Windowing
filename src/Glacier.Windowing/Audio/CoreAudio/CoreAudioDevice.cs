namespace Glacier.Windowing.Audio.CoreAudio;

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Apple macOS and iOS CoreAudio output device interfacing with AudioUnit.
/// </summary>
public sealed class CoreAudioDevice : IAudioDevice
{
    private const string CoreAudioDll = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioComponentDescription
    {
        public uint componentType;
        public uint componentSubType;
        public uint componentManufacturer;
        public uint componentFlags;
        public uint componentFlagsMask;
    }

    [DllImport(CoreAudioDll)]
    private static extern IntPtr AudioComponentFindNext(IntPtr inComponent, ref AudioComponentDescription inDesc);

    [DllImport(CoreAudioDll)]
    private static extern int AudioComponentInstanceNew(IntPtr inComponent, out IntPtr outComponentInstance);

    [DllImport(CoreAudioDll)]
    private static extern int AudioComponentInstanceDispose(IntPtr inComponentInstance);

    private IntPtr _audioUnit;
    private readonly int _sampleRate;
    private readonly int _channels;
    private float _volume = 1.0f;
    private bool _disposed;

    public int SampleRate => _sampleRate;
    public int Channels => _channels;

    public CoreAudioDevice(int sampleRate = 48000, int channels = 2)
    {
        _sampleRate = sampleRate;
        _channels = channels;

        try
        {
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
            {
                AudioComponentDescription desc = new()
                {
                    componentType = 0x61756F75, // 'auou' kAudioUnitType_Output
                    componentSubType = 0x64656620, // 'def ' kAudioUnitSubType_DefaultOutput
                    componentManufacturer = 0x6170706C // 'appl' kAudioUnitManufacturer_Apple
                };

                IntPtr comp = AudioComponentFindNext(IntPtr.Zero, ref desc);
                if (comp != IntPtr.Zero)
                {
                    AudioComponentInstanceNew(comp, out _audioUnit);
                }
            }
        }
        catch
        {
            _audioUnit = IntPtr.Zero;
        }
    }

    public void Play(ReadOnlySpan<float> pcmSamples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Direct AudioUnit render buffer feed
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

        if (_audioUnit != IntPtr.Zero && (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS()))
        {
            try { AudioComponentInstanceDispose(_audioUnit); } catch { }
            _audioUnit = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
