using Microsoft.Playwright;

namespace AutoRetrieval.Services;

public class RetrievalService
{
    private readonly Action<string> _log;
    public RetrievalService(Action<string>? log = null) => _log = log ?? Console.WriteLine;
    private void Log(string message = "") => _log(message);

    public async Task OpenAssignmentAsync(IPage page)
    {
        var retrieveButton =
            page.Locator("button")
                .Filter(new LocatorFilterOptions
                {
                    HasText = "Retrieve"
                })
                .First;

        Log("Waiting for Retrieve button...");

        await retrieveButton.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 120_000
            });

        Log("Clicking Retrieve...");
        await retrieveButton.ClickAsync();

        var assignmentOption =
            page.Locator(
                "#mi-menu-option-assignment-retrieve-menu-item");

        Log("Waiting for Assignment option...");

        await assignmentOption.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000
            });

        Log("Clicking Assignment...");
        await assignmentOption.ClickAsync();

        var claimInput =
            page.Locator("#assignment-pull__claim-number-input");

        Log(
            "Waiting for Retrieve Assignment window...");

        await claimInput.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000
            });

        Log(
            "Retrieve Assignment window opened.");
    }


    public async Task<bool> RetrieveAssignmentAsync(
        IPage page,
        string claimNumber,
        string registrationNumber)
    {
        // ===========================
        // Select ICBC
        // ===========================

        var carrierDropdown =
            page.Locator("#assignment-pull__carrier-list");

        Log(
            "Opening insurance company dropdown...");

        await carrierDropdown.ClickAsync();

        var icbcOption =
            page.GetByText(
                "I C B C - F169330",
                new PageGetByTextOptions
                {
                    Exact = true
                });

        Log("Waiting for ICBC option...");

        await icbcOption.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000
            });

        Log("Selecting ICBC...");
        await icbcOption.ClickAsync();

        Log("ICBC selected.");


        // ===========================
        // Wait for ICBC Form
        // ===========================

        var registrationInput =
            page.Locator(
                "#assignment-pull__bc-registration-number-input");

        Log(
            "Waiting for ICBC-specific form...");

        await registrationInput.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 30_000
            });


        // ===========================
        // Form Elements
        // ===========================

        var claimInput =
            page.Locator(
                "#assignment-pull__claim-number-input");

        var termsCheckbox =
            page.Locator(
                "#assignment-pull__terms-and-condition-checkbox");

        var retrieveButton =
            page.Locator(
                "#assignment-pull__retrieve-assignment-button");


        // ===========================
        // Fill Assignment Information
        // ===========================

        Log(
            $"Entering claim number: {claimNumber}");

        await claimInput.FillAsync(claimNumber);

        Log(
            $"Entering BC registration number: {registrationNumber}");

        await registrationInput.FillAsync(
            registrationNumber);


        // ===========================
        // Terms & Conditions
        // ===========================

        Log(
            "Accepting Terms & Conditions...");

        if (!await termsCheckbox.IsCheckedAsync())
        {
            await termsCheckbox.CheckAsync();
        }


        // ===========================
        // Retrieve Assignment
        // ===========================

        Log(
            "Clicking Retrieve Assignment...");

        await retrieveButton.ClickAsync(
            new LocatorClickOptions
            {
                Timeout = 30_000
            });


        // ===========================
        // Wait for Result
        // ===========================

        Log(
            "Waiting for retrieval result...");

        var doneButton =
            page.Locator(
                "#assignment-pull__done-button");

        var warningIcon =
            page.Locator(
                "#assignment-pull__warning-icon");

        var noAssignmentsMessage = page.GetByText(
            "No valid assignments were found for the given search criteria",
            new PageGetByTextOptions { Exact = true });

        try
        {
            // Check for a result for up to 30 seconds.
            for (int i = 0; i < 30; i++)
            {
                // Failure dialogs also contain Done. Check failures first
                // and leave the dialog untouched for manual review.
                bool noAssignments = await noAssignmentsMessage.IsVisibleAsync();
                if (noAssignments || await warningIcon.IsVisibleAsync())
                {
                    Log();
                    Log(noAssignments
                        ? "No valid assignments were found for the given search criteria."
                        : "Assignment could not be retrieved.");
                    Log("Automation stopped. Leaving retrieval result open for review.");
                    return false;
                }

                // ===========================
                // Successful Retrieval
                // ===========================

                if (await doneButton.IsVisibleAsync())
                {
                    Log();
                    Log(
                        "Assignment retrieved successfully.");

                    Log(
                        "Closing retrieval window...");

                    await doneButton.ClickAsync(
                        new LocatorClickOptions
                        {
                            Timeout = 30_000
                        });

                    // Mitchell needs a few seconds after
                    // retrieval before navigating elsewhere.

                    Log(
                        "Waiting for Mitchell to finish processing...");

                    await Task.Delay(
                        TimeSpan.FromSeconds(7));

                    return true;
                }


                // Neither result has appeared yet.
                await Task.Delay(1000);
            }


            // ===========================
            // Unknown / Timeout
            // ===========================

            Log();
            Log(
                "Could not determine retrieval result.");

            Log(
                "Leaving current screen open for review.");

            return false;
        }
        catch (Exception ex)
        {
            Log();
            Log(
                $"Retrieval result error: {ex.Message}");

            Log(
                "Leaving current screen open for review.");

            return false;
        }
    }
}
