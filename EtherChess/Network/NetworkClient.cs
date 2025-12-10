using System;
using System.Threading.Tasks;
using SocketIOClient;
using EtherChess.Models;

namespace EtherChess.Network;

public class NetworkClient
{
    private SocketIOClient.SocketIO _client;
    private string _serverUrl;
    private string _token;

    public event Action<string> OnMatchFound;
    public event Action<Move> OnOpponentMove;
    public event Action<string> OnGameEnd;

    public bool IsConnected => _client?.Connected ?? false;

    public NetworkClient(string serverUrl, string token)
    {
        _serverUrl = serverUrl;
        _token = token;
        _client = new SocketIOClient.SocketIO(_serverUrl);

        _client.OnConnected += async (sender, e) =>
        {
            Console.WriteLine("Connected to server");
        };

        _client.On("match_found", response =>
        {
            string gameId = response.GetValue<string>();
            OnMatchFound?.Invoke(gameId);
        });

        _client.On("opponent_move", response =>
        {
            var move = response.GetValue<Move>();
            OnOpponentMove?.Invoke(move);
        });

        _client.On("game_end", response =>
        {
            string reason = response.GetValue<string>();
            OnGameEnd?.Invoke(reason);
        });
    }

    public async Task ConnectAsync()
    {
        _client.Options.Auth = new { token = _token };
        await _client.ConnectAsync();
    }

    public async Task FindMatchAsync()
    {
        if (!IsConnected) return;
        await _client.EmitAsync("find_match");
    }

    public async Task SendMoveAsync(Move move)
    {
        if (!IsConnected) return;
        await _client.EmitAsync("make_move", move);
    }

    public async Task DisconnectAsync()
    {
        await _client.DisconnectAsync();
    }
}
