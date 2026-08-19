using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using EtherChess.Models;

namespace EtherChess.Network;

public sealed class PeerSession : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private TcpListener? _listener;
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private int _nextSequence = 1;
    private bool _disposed;

    public bool IsHost { get; private set; }
    public int Port { get; private set; }
    public string LocalUsername { get; private set; } = "";
    public string OpponentUsername { get; private set; } = "Opponent";
    public PieceColor LocalColor { get; private set; } = PieceColor.White;
    public bool IsConnected { get; private set; }

    public event Action<Move>? MoveReceived;
    public event Action<string>? GameEnded;
    public event Action<string>? Disconnected;
    public event Action<double>? MoveAcknowledgedMs;

    public async Task HostAsync(
        int port,
        string username,
        int elo,
        CancellationToken cancellationToken,
        IPAddress? bindAddress = null)
    {
        ThrowIfDisposed();
        IsHost = true;
        LocalUsername = username;
        LocalColor = PieceColor.White;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _listener = new TcpListener(bindAddress ?? IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        using var _ = _cts.Token.Register(() =>
        {
            try { _listener.Stop(); } catch { }
        });

        _client = await _listener.AcceptTcpClientAsync(_cts.Token);
        StopListener();
        ConfigureSocket(_client);
        SetupStreams();
        await CompleteHostHandshakeAsync(elo);
        StartReceiveLoop();
    }

    public async Task JoinAsync(string host, int port, string username, int elo, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        IsHost = false;
        LocalUsername = username;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _client = new TcpClient();
        await _client.ConnectAsync(host, port, _cts.Token);
        ConfigureSocket(_client);
        Port = port;
        SetupStreams();
        await CompleteJoinHandshakeAsync(elo);
        StartReceiveLoop();
    }

    public async Task SendMoveAsync(Move move)
    {
        var sequence = Interlocked.Increment(ref _nextSequence) - 1;
        var message = PeerMessage.FromMove(move, sequence, Stopwatch.GetTimestamp());
        await SendAsync(message);
    }

    public Task SendGameEndAsync(string reason)
    {
        return SendAsync(new PeerMessage
        {
            Type = PeerMessageType.GameEnd,
            Reason = reason,
            Timestamp = Stopwatch.GetTimestamp()
        });
    }

    public Task SendResignAsync()
    {
        return SendAsync(new PeerMessage
        {
            Type = PeerMessageType.Resign,
            Username = LocalUsername,
            Timestamp = Stopwatch.GetTimestamp()
        });
    }

    public Task SendPingAsync()
    {
        return SendAsync(new PeerMessage
        {
            Type = PeerMessageType.Ping,
            Timestamp = Stopwatch.GetTimestamp()
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsConnected = false;

        try { _cts?.Cancel(); } catch { }
        StopListener();
        try { _client?.Close(); } catch { }
        try { _reader?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _cts?.Dispose(); } catch { }
        _writeLock.Dispose();
    }

    private static void ConfigureSocket(TcpClient client)
    {
        client.NoDelay = true;
        client.Client.NoDelay = true;
    }

    private void SetupStreams()
    {
        var stream = _client!.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
            NewLine = "\n"
        };
    }

    private async Task CompleteHostHandshakeAsync(int elo)
    {
        var hello = await ReadMessageAsync(_cts!.Token);
        if (!string.Equals(hello.Type, PeerMessageType.Hello, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Handshake P2P invalide: hello attendu.");
        }

        OpponentUsername = string.IsNullOrWhiteSpace(hello.Username) ? "Opponent" : hello.Username;
        await SendAsync(new PeerMessage
        {
            Type = PeerMessageType.Welcome,
            Username = LocalUsername,
            Elo = elo,
            Color = PieceColor.Black.ToString(),
            Timestamp = Stopwatch.GetTimestamp()
        });
        IsConnected = true;
    }

    private async Task CompleteJoinHandshakeAsync(int elo)
    {
        await SendAsync(new PeerMessage
        {
            Type = PeerMessageType.Hello,
            Username = LocalUsername,
            Elo = elo,
            Timestamp = Stopwatch.GetTimestamp()
        });

        var welcome = await ReadMessageAsync(_cts!.Token);
        if (!string.Equals(welcome.Type, PeerMessageType.Welcome, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Handshake P2P invalide: welcome attendu.");
        }

        OpponentUsername = string.IsNullOrWhiteSpace(welcome.Username) ? "Host" : welcome.Username;
        LocalColor = Enum.TryParse<PieceColor>(welcome.Color, true, out var color)
            ? color
            : PieceColor.Black;
        IsConnected = true;
    }

    private void StartReceiveLoop()
    {
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts!.Token));
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await ReadMessageAsync(cancellationToken);
                await HandleMessageAsync(message);
            }
        }
        catch (OperationCanceledException)
        {
            RaiseDisconnected("Connexion fermée.");
        }
        catch (Exception)
        {
            RaiseDisconnected("Adversaire déconnecté.");
        }
    }

    private async Task HandleMessageAsync(PeerMessage message)
    {
        switch (message.Type)
        {
            case PeerMessageType.Move:
                MoveReceived?.Invoke(message.ToMove());
                await SendAsync(new PeerMessage
                {
                    Type = PeerMessageType.Ack,
                    Timestamp = message.Timestamp,
                    Sequence = message.Sequence
                });
                break;
            case PeerMessageType.Ack:
                MoveAcknowledgedMs?.Invoke(ElapsedMilliseconds(message.Timestamp));
                break;
            case PeerMessageType.GameEnd:
                GameEnded?.Invoke(string.IsNullOrWhiteSpace(message.Reason) ? "Partie terminée" : message.Reason);
                break;
            case PeerMessageType.Resign:
                GameEnded?.Invoke($"{message.Username} a abandonné.");
                break;
            case PeerMessageType.Ping:
                await SendAsync(new PeerMessage
                {
                    Type = PeerMessageType.Pong,
                    Timestamp = message.Timestamp
                });
                break;
            case PeerMessageType.Pong:
                MoveAcknowledgedMs?.Invoke(ElapsedMilliseconds(message.Timestamp));
                break;
        }
    }

    private async Task<PeerMessage> ReadMessageAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
        {
            throw new InvalidOperationException("Flux P2P non initialisé.");
        }

        var line = await _reader.ReadLineAsync(cancellationToken);
        if (line is null)
        {
            throw new EndOfStreamException("Connexion P2P fermée.");
        }

        var message = JsonSerializer.Deserialize<PeerMessage>(line, JsonOptions);
        if (message is null || string.IsNullOrWhiteSpace(message.Type))
        {
            throw new InvalidOperationException("Message P2P illisible.");
        }

        return message;
    }

    private async Task SendAsync(PeerMessage message)
    {
        if (_writer is null || _disposed)
        {
            return;
        }

        try
        {
            var payload = JsonSerializer.Serialize(message, JsonOptions);
            await _writeLock.WaitAsync();
            try
            {
                if (_disposed || _writer is null)
                {
                    return;
                }

                await _writer.WriteLineAsync(payload);
            }
            finally
            {
                try { _writeLock.Release(); } catch (ObjectDisposedException) { }
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void RaiseDisconnected(string reason)
    {
        if (!IsConnected)
        {
            return;
        }

        IsConnected = false;
        Disconnected?.Invoke(reason);
    }

    private static double ElapsedMilliseconds(long timestamp)
    {
        var elapsed = Stopwatch.GetElapsedTime(timestamp);
        return elapsed.TotalMilliseconds;
    }

    private void StopListener()
    {
        if (_listener is null)
        {
            return;
        }

        try { _listener.Stop(); } catch { }
        _listener = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
