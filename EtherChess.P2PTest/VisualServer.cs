using System.Net;
using System.Text;
using System.Text.Json;
using EtherChess.Models;

namespace EtherChess.P2PTest;

internal static class VisualServer
{
    public static async Task RunAsync(int port)
    {
        var prefix = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        Console.WriteLine($"P2P visual test listening on {prefix}");

        while (true)
        {
            var context = await listener.GetContextAsync();
            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private static async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (path == "/stream")
            {
                await HandleStreamAsync(context);
                return;
            }

            await ServeIndexAsync(context);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Visual server error: {ex.Message}");
            try { context.Response.Abort(); } catch { }
        }
    }

    private static async Task ServeIndexAsync(HttpListenerContext context)
    {
        var htmlPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        var bytes = await File.ReadAllBytesAsync(htmlPath);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static async Task HandleStreamAsync(HttpListenerContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";

        async Task Emit(object payload)
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
            await context.Response.OutputStream.WriteAsync(bytes);
            await context.Response.OutputStream.FlushAsync();
        }

        await MatchRunner.RunVisualAsync(Emit);
        await Task.Delay(1500);
        context.Response.Close();
    }
}

internal static class BoardVisual
{
    public static string Squares(Board board)
    {
        var chars = new char[64];
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var piece = board.GetPiece(r, c);
                chars[r * 8 + c] = piece.IsEmpty ? ' ' : piece.GetFenChar();
            }
        }

        return new string(chars);
    }
}
