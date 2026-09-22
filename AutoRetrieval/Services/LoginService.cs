using Microsoft.Playwright;

namespace AutoRetrieval.Services;

public class LoginService
{
    private const string LoginUrl =
        "https://loginca.mymitchell.com/enterprise/authorization/m1/login?app=connect";

    private readonly string _username;
    private readonly string _password;
    private readonly Action<string> _log;

    public LoginService(string username, string password, Action<string>? log = null)
    {
        _username = username;
        _password = password;
        _log = log ?? Console.WriteLine;
    }

    public async Task LoginAsync(IPage page)
    {
        _log("Opening Mitchell login page...");
        await page.GotoAsync(
            LoginUrl,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 120_000
            });

        var usernameField = page.Locator("#inputUserId");
        var passwordField = page.Locator("#inputPassword");
        var signInButton = page.Locator("#submitbutton");

        _log("Waiting for login fields...");
        await usernameField.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible
            });

        _log("Filling login credentials...");
        await usernameField.FillAsync(_username);
        await passwordField.FillAsync(_password);

        _log("Submitting login...");
        await signInButton.ClickAsync();

        _log("Waiting for login response...");
        await page.Locator("#loginForm").WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Detached,
                Timeout = 120_000
            });
    }
}
