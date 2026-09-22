using Microsoft.Playwright;

namespace AutoRetrieval.Services;

public class NavigationService
{
    private readonly Action<string> _log;
    public NavigationService(Action<string>? log = null) => _log = log ?? Console.WriteLine;
    private void Log(string message = "") => _log(message);

    public async Task NavigateToJobsAsync(IPage page)
    {
        var jobsButton = page.Locator("#mi-shell_nav_Jobs");
        var searchBox = page.Locator("#miSearchBoxTextBox");

        Log("Waiting for Jobs navigation...");
        await jobsButton.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = 120_000
        });
        Log("Opening Jobs...");
        await jobsButton.ClickAsync();
        try
        {
            await searchBox.WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5_000
            });
        }
        catch (TimeoutException)
        {
            Log("Jobs dropdown opened. Clicking Jobs again...");
            await jobsButton.ClickAsync();
            await searchBox.WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 120_000
            });
        }
        Log("Jobs opened.");
    }

    public async Task<bool> SearchClaimAsync(IPage page, string claimNumber)
    {
        var searchBox = page.Locator("#miSearchBoxTextBox");
        var searchButton = page.Locator("#miSearchBoxSearchBtn");
        var results = page.Locator("[id^='job-list__item--']");

        Log($"Searching for claim: {claimNumber}");
        await searchBox.FillAsync(claimNumber);
        await searchButton.ClickAsync();
        Log("Waiting for search results...");
        try
        {
            await results.First.WaitForAsync(new()
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5_000
            });
        }
        catch (TimeoutException)
        {
            return false;
        }

        int resultCount = await results.CountAsync();
        if (resultCount == 0) return false;
        Log($"Found {resultCount} result(s).");
        Log("Opening first result...");
        await results.First.ClickAsync();
        Log("Job opened.");
        return true;
    }
}
