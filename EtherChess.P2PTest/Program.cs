if (args.Contains("--visual"))
{
    var port = 5088;
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--port" && int.TryParse(args[i + 1], out var parsed))
        {
            port = parsed;
        }
    }

    await EtherChess.P2PTest.VisualServer.RunAsync(port);
    return;
}

await EtherChess.P2PTest.MatchRunner.RunHeadlessAsync();
