using Microsoft.Playwright;

namespace AutoRetrieval.Services;

public sealed class RetrievalWorkflow
{
    public async Task RunAsync(RetrievalRequest request, string envPath, Action<string> log, CancellationToken token)
    {
        if (!RetrievalRequest.TryNormalizeClaim(request.ClaimNumber, out string claim) ||
            !RetrievalRequest.IsValidRegistration(request.RegistrationNumber))
            throw new ArgumentException("Invalid claim or registration number.");
        // Read credentials without changing the orchestrator's process environment.
        var values = File.Exists(envPath)
            ? DotNetEnv.Env.NoEnvVars().Load(envPath).ToDictionary(x => x.Key, x => x.Value)
            : new Dictionary<string, string>();
        string? user = values.GetValueOrDefault("CL_USERNAME") ?? Environment.GetEnvironmentVariable("CL_USERNAME");
        string? password = values.GetValueOrDefault("CL_PASSWORD") ?? Environment.GetEnvironmentVariable("CL_PASSWORD");
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"Missing credentials in {envPath}.");
        token.ThrowIfCancellationRequested();
        using var playwright = await Playwright.CreateAsync();
        log("Opening Chromium...");
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        browser.Disconnected += (_, _) => closed.TrySetResult();
        var page = await browser.NewPageAsync();
        page.Close += (_, _) => closed.TrySetResult();
        page.SetDefaultTimeout(120_000);
        using var registration = token.Register(() => _ = CloseAsync(browser));
        try
        {
            token.ThrowIfCancellationRequested();
            log("Logging into Mitchell Connect...");
            await new LoginService(user, password, log).LoginAsync(page);
            log("Login form closed. Opening assignment retrieval...");
            token.ThrowIfCancellationRequested();
            var retrieval = new RetrievalService(log);
            await retrieval.OpenAssignmentAsync(page);
            token.ThrowIfCancellationRequested();
            if (await retrieval.RetrieveAssignmentAsync(page, claim, request.RegistrationNumber))
            {
                token.ThrowIfCancellationRequested();
                var navigation = new NavigationService(log);
                await navigation.NavigateToJobsAsync(page);
                bool found = await navigation.SearchClaimAsync(page, claim);
                log(found ? "Workflow complete. Claim opened." : "Claim not found. Review the browser.");
            }
            else log("Retrieval unsuccessful. Review the browser result.");
        }
        catch (Exception ex) when (!token.IsCancellationRequested) { log($"Workflow failed: {ex.Message}"); }
        finally
        {
            if (!token.IsCancellationRequested && browser.IsConnected && !page.IsClosed)
            {
                log("Browser remains open. Close it or click Stop when finished.");
                await closed.Task.WaitAsync(token);
            }
        }
        token.ThrowIfCancellationRequested();
    }

    private static async Task CloseAsync(IBrowser browser)
    {
        try { await browser.CloseAsync(); } catch (PlaywrightException) { }
    }
}
