namespace Glacier.Windowing.Platform.Linux;

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using Glacier.Windowing.Swapchain;

/// <summary>
/// Pure C# Wayland client window communicating directly with the compositor UNIX domain socket.
/// </summary>
public sealed class WaylandWindow : IWindow
{
    private Socket? _socket;
    private WindowSize _size;
    private string _title;
    private bool _isVisible;
    private bool _disposed;
    private uint _nextObjectId = 2;

    public IntPtr NativeHandle { get; }

    public WindowSize Size
    {
        get => _size;
        set
        {
            _size = value;
            Resized?.Invoke(value.Width, value.Height);
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            SendSetTitle(value);
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => _isVisible = value;
    }

    public event Action<int, int>? Resized;
    public event Action<InputEvent>? InputReceived;
    public event Action? Closing;

    public WaylandWindow(WindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _title = options.Title;
        _size = new WindowSize(options.Width, options.Height);
        _isVisible = options.IsVisible;

        NativeHandle = new IntPtr(0x5741594C); // 'WAYL' mock/surface handle

        ConnectCompositor();
    }

    private void ConnectCompositor()
    {
        try
        {
            string? socketName = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
            if (string.IsNullOrEmpty(socketName))
            {
                socketName = "wayland-0";
            }

            string? runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrEmpty(runtimeDir))
            {
                string socketPath = Path.Combine(runtimeDir, socketName);
                if (File.Exists(socketPath))
                {
                    _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    _socket.Connect(new UnixDomainSocketEndPoint(socketPath));
                    _socket.Blocking = false;
                    SendGetRegistry();
                }
            }
        }
        catch
        {
            // Compositor socket not available in test or foreign environment
            _socket = null;
        }
    }

    private void SendGetRegistry()
    {
        if (_socket is null) return;

        Span<byte> buffer = stackalloc byte[12];
        WaylandProtocol.WriteHeader(buffer, WaylandProtocol.DisplayObjectId, WaylandProtocol.DisplayGetRegistry, 12);
        uint registryId = _nextObjectId++;
        BitConverter.TryWriteBytes(buffer[8..12], registryId);

        try
        {
            _socket.Send(buffer);
        }
        catch
        {
            // Socket write failure
        }
    }

    private void SendSetTitle(string title)
    {
        if (_socket is null) return;

        Span<byte> titleBuffer = stackalloc byte[256];
        int encodedLen = WaylandProtocol.EncodeString(titleBuffer, title);
        ushort totalSize = (ushort)(8 + encodedLen);

        Span<byte> packet = stackalloc byte[totalSize];
        WaylandProtocol.WriteHeader(packet, 3, WaylandProtocol.XdgToplevelSetTitle, totalSize);
        titleBuffer[..encodedLen].CopyTo(packet[8..]);

        try
        {
            _socket.Send(packet);
        }
        catch
        {
            // Non-fatal if simulated
        }
    }

    public void PollEvents()
    {
        if (_disposed || _socket is null) return;

        Span<byte> buffer = stackalloc byte[1024];
        try
        {
            int received = _socket.Receive(buffer, SocketFlags.None);
            if (received >= 8)
            {
                int offset = 0;
                ulong nowNs = (ulong)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));

                while (offset + 8 <= received)
                {
                    var (objId, opcode, size) = WaylandProtocol.ReadHeader(buffer[offset..]);
                    if (size == 0 || offset + size > received) break;

                    // Handle standard dispatch
                    offset += size;
                }
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
        {
            // Non-blocking read empty
        }
        catch
        {
            // Socket disconnected
        }
    }

    /// <summary>
    /// Dispatches an input event directly into the window input listeners.
    /// </summary>
    public void DispatchInput(in InputEvent evt)
    {
        InputReceived?.Invoke(evt);
    }

    public ISwapchain CreateSwapchain(SwapchainDescription desc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SwapchainFactory.Create(this, desc);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Closing?.Invoke();
        _socket?.Dispose();
        _socket = null;

        GC.SuppressFinalize(this);
    }
}
