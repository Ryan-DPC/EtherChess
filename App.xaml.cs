using System.Windows;

namespace EtherChess;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? userJson = Environment.GetEnvironmentVariable("ETHER_USER");
        string? token = Environment.GetEnvironmentVariable("ETHER_TOKEN");

        Log($"Startup Args: {string.Join(" ", e.Args)}");
        Log($"Env USER present: {!string.IsNullOrWhiteSpace(userJson)}");
        Log($"Env TOKEN: {(string.IsNullOrEmpty(token) ? "NULL" : "PRESENT")}");

        // Fallback to args if env vars are missing
        bool isDev = false;
        string? devName = null;
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (string.IsNullOrEmpty(userJson) && e.Args[i] == "--user" && i + 1 < e.Args.Length)
                userJson = e.Args[i + 1];
            if (string.IsNullOrEmpty(token) && e.Args[i] == "--token" && i + 1 < e.Args.Length)
                token = e.Args[i + 1];
            if (e.Args[i] == "--dev")
                isDev = true;
            if ((e.Args[i] == "--name" || e.Args[i] == "--player") && i + 1 < e.Args.Length)
                devName = e.Args[i + 1];
        }

        if (isDev && (string.IsNullOrEmpty(userJson) || string.IsNullOrEmpty(token)))
        {
            var username = string.IsNullOrWhiteSpace(devName) ? "DevUser" : devName.Trim();
            Log($"Dev mode enabled. Using dummy credentials for {username}.");
            userJson = $"{{\"username\": \"{username}\", \"elo\": 1500}}";
            token = "dev-token";
        }

        var mainWindow = new EtherChess.Views.MainWindow();
        var viewModel = new EtherChess.ViewModels.MainViewModel();
        
        if (!string.IsNullOrEmpty(userJson) && !string.IsNullOrEmpty(token))
        {
            Log($"Found User Data. Initializing ViewModel...");
            viewModel.Initialize(userJson, token);
        }
        else 
        {
             Log("No user data found. Staying as Guest.");
        }

        mainWindow.DataContext = viewModel;
        mainWindow.Show();
    }

    public static void Log(string message)
    {
        try
        {
            System.IO.File.AppendAllText("debug_log.txt", $"{DateTime.Now}: {message}\n");
        }
        catch { }
    }
}

