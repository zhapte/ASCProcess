using Microsoft.Playwright;

namespace CollisionLinkDownloader.Services;

public class LoginService
{
    private const string LoginUrl =
        "https://loginca.mymitchell.com/enterprise/authorization/m1/login?app=connect";

    private readonly string _username;
    private readonly string _password;

    public LoginService(string username, string password)
    {
        _username = username;
        _password = password;
    }

    public async Task LoginAsync(IPage page)
    {
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

        await usernameField.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible
            });

        await usernameField.FillAsync(_username);
        await passwordField.FillAsync(_password);

        await signInButton.ClickAsync();

        await page.Locator("#loginForm").WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Detached,
                Timeout = 120_000
            });
    }
}