using System.Text.RegularExpressions;

namespace AutoRetrieval.Services;

public sealed record RetrievalRequest(
    string ClaimNumber,
    string RegistrationNumber)
{
    public static async Task<RetrievalRequest> PromptAsync(
        CancellationToken cancellationToken = default)
    {
        string claim;

        while (true)
        {
            Console.Write(
                "Enter claim number " +
                "(e.g. AB12345-6 or AB123456; -A is added if no suffix is provided): ");

            string input =
                await ReadAsync(cancellationToken);

            if (TryNormalizeClaim(input, out claim))
            {
                break;
            }

            Console.WriteLine(
                "Use format AB12345-6 or AB123456, optionally followed by a letter suffix (e.g. -B).");
        }


        string registration;

        while (true)
        {
            Console.Write(
                "Enter registration number (8 digits): ");

            registration =
                await ReadAsync(cancellationToken);

            if (IsValidRegistration(registration))
            {
                break;
            }

            Console.WriteLine(
                "Registration must contain exactly 8 digits.");
        }

        return new RetrievalRequest(
            claim,
            registration);
    }


    public static bool TryNormalizeClaim(
        string input,
        out string claim)
    {
        string value =
            input.Trim().ToUpperInvariant();

        // Separators are optional, but the claim structure remains strict.
        var match = Regex.Match(
            value,
            @"\A([A-Z]{2}[0-9]{5})-?([0-9])(?:-?([A-Z]))?\z");
        if (!match.Success)
        {
            claim = string.Empty;
            return false;
        }

        // Always send Mitchell the canonical dashed form; default suffix is A.
        string suffix = match.Groups[3].Success ? match.Groups[3].Value : "A";
        claim = $"{match.Groups[1].Value}-{match.Groups[2].Value}-{suffix}";
        return true;
    }


    public static bool IsValidRegistration(
        string input)
    {
        return Regex.IsMatch(
            input,
            @"\A[0-9]{8}\z");
    }


    private static async Task<string> ReadAsync(
        CancellationToken cancellationToken)
    {
        return
            (await Console.In.ReadLineAsync(
                cancellationToken))?.Trim()
            ?? throw new EndOfStreamException(
                "Input closed before both values were supplied.");
    }
}
