namespace Glacier.Windowing.Audio;

using System;

/// <summary>
/// Factory for instantiating platform-native low-latency audio devices and lock-free streams.
/// </summary>
public static class AudioFactory
{
    /// <summary>
    /// Creates the optimal hardware audio device for the current host operating system.
    /// </summary>
    public static IAudioDevice CreateDefaultDevice(int sampleRate = 48000, int channels = 2)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return new Wasapi.WasapiAudioDevice(sampleRate, channels);
            }
            catch
            {
                return new Simulated.SimulatedAudioDevice(sampleRate, channels);
            }
        }
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
        {
            return new CoreAudio.CoreAudioDevice(sampleRate, channels);
        }
        if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
        {
            return new Alsa.AlsaAudioDevice(sampleRate, channels);
        }

        return new Simulated.SimulatedAudioDevice(sampleRate, channels);
    }

    /// <summary>
    /// Creates an ultra-low latency streaming channel backed by a lock-free SPSC ring buffer.
    /// </summary>
    public static IAudioStream CreateStream(int sampleRate = 48000, int channels = 2, int bufferCapacity = 16384)
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return new Wasapi.WasapiAudioStream(sampleRate, channels, bufferCapacity);
            }
            catch
            {
                return new Simulated.SimulatedAudioDevice(sampleRate, channels, bufferCapacity);
            }
        }

        return new Simulated.SimulatedAudioDevice(sampleRate, channels, bufferCapacity);
    }

    /// <summary>
    /// Creates a simulated audio device for automated testing or headless execution.
    /// </summary>
    public static Simulated.SimulatedAudioDevice CreateSimulated(int sampleRate = 48000, int channels = 2, int bufferCapacity = 16384)
    {
        return new Simulated.SimulatedAudioDevice(sampleRate, channels, bufferCapacity);
    }
}
