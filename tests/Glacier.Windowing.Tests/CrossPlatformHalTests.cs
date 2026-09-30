namespace Glacier.Windowing.Tests;

using System;
using System.Text;
using Glacier.Windowing.Platform.Linux;
using Glacier.Windowing.Platform.Mac;
using Glacier.Windowing.Platform.Mobile;
using Xunit;

public class CrossPlatformHalTests
{
    [Fact]
    public void WaylandProtocol_HeaderSerialization_RoundtripsCorrectly()
    {
        Span<byte> buffer = stackalloc byte[8];
        uint objId = 42;
        ushort opcode = 3;
        ushort size = 28;

        WaylandProtocol.WriteHeader(buffer, objId, opcode, size);

        var (parsedObj, parsedOpcode, parsedSize) = WaylandProtocol.ReadHeader(buffer);
        Assert.Equal(objId, parsedObj);
        Assert.Equal(opcode, parsedOpcode);
        Assert.Equal(size, parsedSize);
    }

    [Fact]
    public void WaylandProtocol_StringEncoding_PadsTo4Bytes()
    {
        Span<byte> buffer = stackalloc byte[64];
        string title = "Glacier"; // 7 chars + 1 null = 8 bytes -> 4-byte aligned!
        int encodedLen = WaylandProtocol.EncodeString(buffer, title);

        // 4 bytes length prefix (8) + 8 bytes "Glacier\0" = 12 bytes total
        Assert.Equal(12, encodedLen);
        uint stringLen = BitConverter.ToUInt32(buffer[0..4]);
        Assert.Equal(8U, stringLen);

        string titleOdd = "Hi"; // 2 chars + 1 null = 3 bytes -> total 4 + 3 = 7 -> padded to 8 bytes
        int encodedOdd = WaylandProtocol.EncodeString(buffer, titleOdd);
        Assert.Equal(8, encodedOdd);
    }

    [Fact]
    public void AndroidNativeWindow_TouchDispatch_TranslatesToInputEvents()
    {
        using var window = new AndroidNativeWindow(new WindowOptions { Width = 1080, Height = 2400 });
        var events = new List<InputEvent>();
        window.InputReceived += evt => events.Add(evt);

        TouchSnapshot[] touches =
        [
            new TouchSnapshot(1, 100, 200, 0.8f, TouchPhase.Began, 1000),
            new TouchSnapshot(1, 110, 210, 0.85f, TouchPhase.Moved, 2000),
            new TouchSnapshot(1, 120, 220, 0.0f, TouchPhase.Ended, 3000)
        ];

        window.DispatchTouch(touches);

        Assert.Equal(3, events.Count);
        Assert.Equal(InputEventType.TouchStart, events[0].Type);
        Assert.Equal(1, events[0].KeyOrButton);
        Assert.Equal(100f, events[0].X);

        Assert.Equal(InputEventType.TouchMove, events[1].Type);
        Assert.Equal(InputEventType.TouchEnd, events[2].Type);
    }

    [Fact]
    public void IosUiWindow_ProMotionAndTouch_FunctionsCorrectly()
    {
        using var window = new IosUiWindow(new WindowOptions { Width = 1179, Height = 2556 });
        Assert.Equal(120.0, window.DisplayRefreshRate);

        window.SetPreferredFramesPerSecond(60);
        Assert.Equal(60.0, window.DisplayRefreshRate);

        var events = new List<InputEvent>();
        window.InputReceived += evt => events.Add(evt);

        TouchSnapshot[] touches =
        [
            new TouchSnapshot(0, 50, 50, 1.0f, TouchPhase.Began, 500)
        ];

        window.DispatchTouches(touches);

        Assert.Single(events);
        Assert.Equal(InputEventType.TouchStart, events[0].Type);
    }

    [Fact]
    public void X11AndCocoaWindows_CreationAndDisposal_Succeeds()
    {
        using var x11 = new X11Window(new WindowOptions { Width = 800, Height = 600 });
        Assert.NotNull(x11);
        Assert.Equal(800, x11.Size.Width);
        Assert.Equal(600, x11.Size.Height);

        using var cocoa = new CocoaWindow(new WindowOptions { Width = 1024, Height = 768 });
        Assert.NotNull(cocoa);
        Assert.Equal(1024, cocoa.Size.Width);
        Assert.Equal(768, cocoa.Size.Height);
    }
}
