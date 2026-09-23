using Microsoft.Playwright;

namespace CollisionLinkDownloader.Services;

public sealed class DownloadWorkflow
{
    public async Task RunAsync(string claim, string envPath, Action<string> log, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(claim)) throw new ArgumentException("Claim number is required.");
        Dictionary<string, string> values;
        try
        {
            values = File.Exists(envPath)
                ? DotNetEnv.Env.NoEnvVars().Load(envPath).ToDictionary(x => x.Key, x => x.Value)
                : new Dictionary<string, string>();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Could not read credentials from {envPath}. Expected CL_USERNAME=... and CL_PASSWORD=... lines.",
                exception);
        }

        string? user = values.GetValueOrDefault("CL_USERNAME") ?? Environment.GetEnvironmentVariable("CL_USERNAME");
        string? password = values.GetValueOrDefault("CL_PASSWORD") ?? Environment.GetEnvironmentVariable("CL_PASSWORD");
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"Missing credentials in {envPath}.");
        token.ThrowIfCancellationRequested();
        using var playwright = await Playwright.CreateAsync();
        log("Starting headless Chromium...");
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        using var registration = token.Register(() => _ = CloseAsync(browser));
        token.ThrowIfCancellationRequested();
        var page = await browser.NewPageAsync(new() { AcceptDownloads = true });
        page.SetDefaultTimeout(120_000);
        log("Logging into Mitchell Connect...");
        await new LoginService(user, password).LoginAsync(page);
        token.ThrowIfCancellationRequested();
        log("Login successful.");
        var navigation = new NavigationService(log);
        await navigation.NavigateToJobsAsync(page);
        token.ThrowIfCancellationRequested();
        if (!await navigation.SearchClaimAsync(page, claim.Trim()))
            throw new InvalidOperationException($"Claim {claim} was not found. Correct the claim and try again.");
        token.ThrowIfCancellationRequested();
        var download = new DownloadService(log);
        await download.OpenEstimateCardAsync(page);
        Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
        string zip = await download.DownloadEmsAsync(page);
        token.ThrowIfCancellationRequested();
        new ExtractService(log).Extract(zip);
        log("CollisionLink download completed successfully.");
    }

    private static async Task CloseAsync(IBrowser browser)
    {
        try { await browser.CloseAsync(); } catch (PlaywrightException) { }
    }
}
