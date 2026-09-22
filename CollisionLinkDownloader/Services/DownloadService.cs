using Microsoft.Playwright;

namespace CollisionLinkDownloader.Services;

public class DownloadService
{
    private readonly Action<string> _log;
    public DownloadService(Action<string>? log = null) => _log = log ?? Console.WriteLine;
    private void Log(string message = "") => _log(message);

    public async Task OpenEstimateCardAsync(IPage page)
    {
        var estimateCard =
            page.Locator("#joboverview__estimate-card__template-card");

        Log("Waiting for estimate card...");

        await estimateCard.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 120_000
            });

        Log("Opening estimate card...");

        await estimateCard.ClickAsync();

        Log("Estimate card opened.");
    }

    public async Task<string> DownloadEmsAsync(IPage page)
    {
        var exportButton =
            page.Locator("#estimate_export_ems_button");

        var downloadEms =
            page.Locator("#list-item-DOWNLOAD_EMS");

        Log("Waiting for EMS menu button...");

        await exportButton.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 120_000
            });

        Log("Opening EMS menu...");

        await exportButton.ClickAsync();

        Log("Waiting for Download EMS option...");

        await downloadEms.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000
            });

        Log("Selecting Download EMS...");

        Log("Starting EMS download...");

        var download = await page.RunAndWaitForDownloadAsync(
            async () =>
            {
                await downloadEms.ClickAsync();
            });

        string downloadsFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                "Downloads");

        string filePath =
            Path.Combine(
                downloadsFolder,
                download.SuggestedFilename);

        await download.SaveAsAsync(filePath);

        Log($"Downloaded: {filePath}");

        Log("Download EMS selected.");

        return filePath;
    }
}
