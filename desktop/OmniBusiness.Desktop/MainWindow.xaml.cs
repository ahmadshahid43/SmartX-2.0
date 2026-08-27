using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace OmniBusiness.Desktop;

public partial class MainWindow : Window
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private readonly string _repoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RefreshStatusAsync();
    }

    private async void RefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
    }

    private void StartApi_Click(object sender, RoutedEventArgs e)
    {
        LaunchScript("run-api.cmd");
    }

    private void StartWeb_Click(object sender, RoutedEventArgs e)
    {
        LaunchScript("run-web.cmd");
    }

    private void OpenDashboard_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("http://localhost:4200/dashboard");
    }

    private void OpenPos_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("http://localhost:4200/pos");
    }

    private void OpenSales_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("http://localhost:4200/sales");
    }

    private void OpenInventory_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("http://localhost:4200/inventory");
    }

    private void OpenUsers_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("http://localhost:4200/users");
    }

    private void OpenBuilder_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("http://localhost:4200/form-builder");
    }

    private void OpenSwagger_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("http://localhost:5163/swagger");
    }

    private async Task RefreshStatusAsync()
    {
        ApiStatusText.Text = await IsHealthyAsync("http://localhost:5163/health")
            ? "Running on http://localhost:5163"
            : "Stopped";

        WebStatusText.Text = await IsHealthyAsync("http://localhost:4200")
            ? "Running on http://localhost:4200"
            : "Stopped";

        FbrModeText.Text = LoadFbrMode();
    }

    private async Task<bool> IsHealthyAsync(string url)
    {
        try
        {
            using var response = await HttpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string LoadFbrMode()
    {
        try
        {
            var appSettingsPath = Path.Combine(_repoRoot, "src", "OmniBusiness.Api", "appsettings.json");
            if (!File.Exists(appSettingsPath))
            {
                return "Unknown";
            }

            using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
            return document.RootElement
                .GetProperty("Fbr")
                .GetProperty("Mode")
                .GetString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private void LaunchScript(string scriptName)
    {
        var fullPath = Path.Combine(_repoRoot, scriptName);
        if (!File.Exists(fullPath))
        {
            MessageBox.Show(
                $"Script not found: {fullPath}",
                "OmniBusiness Desktop",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            WorkingDirectory = _repoRoot,
            UseShellExecute = true
        });
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
