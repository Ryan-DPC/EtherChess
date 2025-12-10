using System.Configuration;
using System.Data;
using System.Windows;

namespace EtherChess;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Manually load resources since InitializeComponent is acting up
        try 
        {
            this.Resources.Add("BooleanToColorConverter", new EtherChess.Converters.BooleanToColorConverter());
            this.Resources.Add("CountToVisibilityConverter", new EtherChess.Converters.CountToVisibilityConverter());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EtherChess] Failed to load resources: {ex.Message}");
        }

        string userJson = Environment.GetEnvironmentVariable("ETHER_USER");
        string token = Environment.GetEnvironmentVariable("ETHER_TOKEN");

        Log($"Startup Args: {string.Join(" ", e.Args)}");
        Log($"Env USER: {userJson}");
        Log($"Env TOKEN: {(string.IsNullOrEmpty(token) ? "NULL" : "PRESENT")}");

        // Fallback to args if env vars are missing
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (string.IsNullOrEmpty(userJson) && e.Args[i] == "--user" && i + 1 < e.Args.Length)
                userJson = e.Args[i + 1];
            if (string.IsNullOrEmpty(token) && e.Args[i] == "--token" && i + 1 < e.Args.Length)
                token = e.Args[i + 1];
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

