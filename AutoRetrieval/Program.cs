using DotNetEnv;
using Microsoft.Playwright;
using AutoRetrieval.Services;


// ===========================
// Environment
// ===========================

string envPath =
    Path.Combine(AppContext.BaseDirectory, ".env");

string projectDirectory =
    Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "../../.."));

if (!File.Exists(envPath) &&
    File.Exists(
        Path.Combine(
            projectDirectory,
            "AutoRetrieval.csproj")))
{
    envPath =
        Path.Combine(
            projectDirectory,
            ".env");
}

if (File.Exists(envPath))
{
    Env.Load(envPath);
}

string? username =
    Environment.GetEnvironmentVariable(
        "CL_USERNAME");

string? password =
    Environment.GetEnvironmentVariable(
        "CL_PASSWORD");

if (string.IsNullOrWhiteSpace(username) ||
    string.IsNullOrWhiteSpace(password))
{
    Console.Error.WriteLine(
        $"Missing CL_USERNAME or CL_PASSWORD. " +
        $"Check {envPath} or set both environment variables.");

    Environment.ExitCode = 1;
    return;
}


// ===========================
// Retrieval Request
// ===========================

RetrievalRequest request;

try
{
    request =
        await RetrievalRequest.PromptAsync();
}
catch (EndOfStreamException exception)
{
    Console.Error.WriteLine(
        exception.Message);

    Environment.ExitCode = 1;
    return;
}

Console.WriteLine(
    $"Claim: {request.ClaimNumber} | " +
    $"Registration: {request.RegistrationNumber}");


// ===========================
// Playwright
// ===========================

using var playwright =
    await Playwright.CreateAsync();

await using var browser =
    await playwright.Chromium.LaunchAsync(
        new BrowserTypeLaunchOptions
        {
            Headless = false
        });

var page =
    await browser.NewPageAsync();

var browserClosed =
    new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);

browser.Disconnected +=
    (_, _) =>
        browserClosed.TrySetResult();

page.Close +=
    (_, _) =>
        browserClosed.TrySetResult();

page.SetDefaultTimeout(120_000);


// ===========================
// Services
// ===========================

var loginService =
    new LoginService(
        username,
        password);

var retrieveService =
    new RetrievalService();

var navigationService =
    new NavigationService();


// ===========================
// Login
// ===========================

Console.WriteLine();
Console.WriteLine(
    "Logging into Mitchell Connect...");

await loginService.LoginAsync(page);

Console.WriteLine(
    "Login successful.");


// ===========================
// Open Retrieve Assignment
// ===========================

Console.WriteLine();
Console.WriteLine(
    "Opening Retrieve Assignment...");

await retrieveService.OpenAssignmentAsync(page);


// ===========================
// Retrieve Assignment
// ===========================

bool retrievalSucceeded =
    await retrieveService.RetrieveAssignmentAsync(
        page,
        request.ClaimNumber,
        request.RegistrationNumber);


// ===========================
// Retrieval Failed
// ===========================

if (!retrievalSucceeded)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(
        "Retrieval was unsuccessful.");

    Console.Error.WriteLine(
        "Automation stopped.");

    Console.Error.WriteLine(
        "The retrieval result has been left open for review.");

    Environment.ExitCode = 1;

    // Keep browser open so the user can
    // review Mitchell's failure message.
    if (browser.IsConnected &&
        !page.IsClosed)
    {
        Console.WriteLine();
        Console.WriteLine(
            "Close the browser window when finished, " +
            "or use Stop in the orchestrator.");

        await browserClosed.Task;
    }

    return;
}


// ===========================
// Retrieval Successful
// ===========================

// RetrieveAssignmentAsync has already:
// 1. confirmed success
// 2. clicked Done
// 3. waited 7 seconds

Console.WriteLine();
Console.WriteLine(
    "Retrieval complete. Continuing to Jobs...");


// ===========================
// Navigate to Jobs
// ===========================

await navigationService.NavigateToJobsAsync(
    page);


// ===========================
// Search Claim
// LAST STEP
// ===========================

Console.WriteLine();
Console.WriteLine(
    $"Searching for claim {request.ClaimNumber}...");

bool found =
    await navigationService.SearchClaimAsync(
        page,
        request.ClaimNumber);

if (found)
{
    Console.WriteLine();
    Console.WriteLine(
        $"Claim search result opened for " +
        $"{request.ClaimNumber}.");

    Console.WriteLine(
        "Automation complete.");
}
else
{
    Console.Error.WriteLine();
    Console.Error.WriteLine(
        $"Claim {request.ClaimNumber} was not found.");

    Console.Error.WriteLine(
        "Review the search in the browser.");

    Environment.ExitCode = 1;
}


// ===========================
// Keep Browser Open
// ===========================

if (browser.IsConnected &&
    !page.IsClosed)
{
    Console.WriteLine();
    Console.WriteLine(
        "Browser left open for inspection.");

    Console.WriteLine(
        "Close the browser window when finished, " +
        "or use Stop in the orchestrator.");

    await browserClosed.Task;
}