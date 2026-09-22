using DotNetEnv;
using Microsoft.Playwright;
using CollisionLinkDownloader.Services;

Env.Load();

string? username =
    Environment.GetEnvironmentVariable("CL_USERNAME");

string? password =
    Environment.GetEnvironmentVariable("CL_PASSWORD");

if (string.IsNullOrWhiteSpace(username) ||
    string.IsNullOrWhiteSpace(password))
{
    Console.WriteLine("Missing CL_USERNAME or CL_PASSWORD.");
    return;
}


// Ask for claim BEFORE opening Chromium
Console.Write("Enter claim number: ");
string? claimNumber = Console.ReadLine()?.Trim();

while (string.IsNullOrWhiteSpace(claimNumber))
{
    Console.WriteLine("Claim number is required.");
    Console.Write("Enter claim number: ");
    claimNumber = Console.ReadLine()?.Trim();
}


using var playwright = await Playwright.CreateAsync();

await using var browser =
    await playwright.Chromium.LaunchAsync(
        new BrowserTypeLaunchOptions
        {
            Headless = true
        });

var page = await browser.NewPageAsync();

page.SetDefaultTimeout(120_000);

var loginService =
    new LoginService(username, password);

var navigationService =
    new NavigationService();

var downloadService =
    new DownloadService();

var extractService =
    new ExtractService();


// Login
Console.WriteLine();
Console.WriteLine("Logging into Mitchell Connect...");

await loginService.LoginAsync(page);

Console.WriteLine("Login successful.");


// Open Jobs
Console.WriteLine();
Console.WriteLine("Opening Jobs...");

await navigationService.NavigateToJobsAsync(page);


// Search until we find a valid claim
while (true)
{
    Console.WriteLine();
    Console.WriteLine($"Searching for claim {claimNumber}...");

    bool claimFound =
        await navigationService.SearchClaimAsync(
            page,
            claimNumber);

    if (claimFound)
        break;

    Console.WriteLine();
    Console.WriteLine($"Claim {claimNumber} was not found.");

    // Only ask again if the previous search failed
    do
    {
        Console.Write("Enter another claim number: ");
        claimNumber = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(claimNumber))
            Console.WriteLine("Claim number is required.");

    } while (string.IsNullOrWhiteSpace(claimNumber));
}


// Claim has been opened
Console.WriteLine();
Console.WriteLine("Claim opened successfully.");

await downloadService.OpenEstimateCardAsync(page);

string zipPath =
    await downloadService.DownloadEmsAsync(page);

extractService.Extract(zipPath);


Console.WriteLine();
Console.WriteLine(
    "CollisionLink download completed successfully.");

Console.WriteLine();
Console.WriteLine("Press ENTER to close.");

Console.ReadLine();