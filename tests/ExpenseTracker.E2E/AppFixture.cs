using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;

namespace ExpenseTracker.E2E;

/// <summary>
/// Starts the real web app in its own process (on a free port, against its own database) and a Chrome
/// browser driven by Playwright. One instance is shared by all the E2E tests.
///
/// The tests only run when the environment variable E2E=1 is set: they need Chrome (or Edge) and SQL Server
/// LocalDB, which a CI runner does not have. Set E2E_HEADED=1 to watch the browser.
/// </summary>
public sealed class AppFixture : IAsyncLifetime
{
    public const string DatabaseName = "ExpenseTrackerE2E";
    private const string MasterConnection = @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True";
    private static readonly string AppConnection = $@"Server=(localdb)\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    private readonly StringBuilder _appLog = new();
    private Process? _app;
    private IPlaywright? _playwright;

    public static bool Enabled => Environment.GetEnvironmentVariable("E2E") == "1";

    public string BaseUrl { get; private set; } = string.Empty;

    public IBrowser Browser { get; private set; } = null!;

    /// <summary>Folder where screenshots are written (ignored by git).</summary>
    public string ArtifactsDir { get; private set; } = string.Empty;

    /// <summary>What the web app printed, to diagnose a failing test.</summary>
    public string AppLog
    {
        get
        {
            lock (_appLog)
                return _appLog.ToString();
        }
    }

    public async Task InitializeAsync()
    {
        if (!Enabled)
            return;

        var root = FindRepositoryRoot();
        ArtifactsDir = Path.Combine(root, "tests", "ExpenseTracker.E2E", "artifacts");
        Directory.CreateDirectory(ArtifactsDir);

        await ResetDatabaseAsync();
        await StartAppAsync(root);
        await StartBrowserAsync();
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.CloseAsync();

        _playwright?.Dispose();

        if (_app is { HasExited: false })
        {
            _app.Kill(entireProcessTree: true);
            await _app.WaitForExitAsync();
        }

        _app?.Dispose();

        // Leave no test database behind (the next run also starts from scratch).
        if (Enabled)
        {
            try
            {
                await ResetDatabaseAsync();
            }
            catch (SqlException)
            {
                // best effort
            }
        }
    }

    // ---------- app process ----------

    private async Task StartAppAsync(string root)
    {
        var configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? "Release" : "Debug";
        var webProject = Path.Combine(root, "src", "Presentation", "ExpenseTracker.Web");
        var dll = Path.Combine(webProject, "bin", configuration, "net10.0", "ExpenseTracker.Web.dll");
        if (!File.Exists(dll))
            throw new FileNotFoundException($"La app web no está compilada ({configuration}). Compile la solución antes de las pruebas E2E.", dll);

        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var startInfo = new ProcessStartInfo("dotnet", $"\"{dll}\"")
        {
            WorkingDirectory = webProject,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; // enables /dev/ui and migrates on start
        startInfo.Environment["ASPNETCORE_URLS"] = BaseUrl;
        startInfo.Environment["ConnectionStrings__ExpenseTracker"] = AppConnection;

        _app = new Process { StartInfo = startInfo };
        _app.OutputDataReceived += (_, e) => Append(e.Data);
        _app.ErrorDataReceived += (_, e) => Append(e.Data);
        _app.Start();
        _app.BeginOutputReadLine();
        _app.BeginErrorReadLine();

        await WaitUntilHealthyAsync();
    }

    private async Task WaitUntilHealthyAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadline)
        {
            if (_app!.HasExited)
                throw new InvalidOperationException($"La app terminó al arrancar (código {_app.ExitCode}).\n{AppLog}");

            try
            {
                var response = await http.GetAsync($"{BaseUrl}/health");
                if (response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }
            catch (TaskCanceledException)
            {
                // slow first request while migrating
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"La app no respondió /health en 90 s.\n{AppLog}");
    }

    private void Append(string? line)
    {
        if (line is null)
            return;

        lock (_appLog)
            _appLog.AppendLine(line);
    }

    // ---------- browser ----------

    private async Task StartBrowserAsync()
    {
        _playwright = await Playwright.CreateAsync();
        var headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1";

        // Uses the Chrome (or Edge) installed on the machine: no browser download is needed.
        try
        {
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "chrome", Headless = headless });
        }
        catch (PlaywrightException)
        {
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = headless });
        }
    }

    // ---------- database ----------

    private static async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(MasterConnection);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
            IF DB_ID('{DatabaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{DatabaseName}];
            END";
        await command.ExecuteNonQueryAsync();
    }

    // ---------- helpers ----------

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExpenseTracker.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró ExpenseTracker.slnx subiendo desde " + AppContext.BaseDirectory);
    }
}

[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<AppFixture>
{
    public const string Name = "E2E";
}
