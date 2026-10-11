namespace GolfSG.Application.Accounts;

public static class ApiPasswordPolicy
{
    public const int MaximumLength = 128;

    // Compile the local testing exception out of Release builds.
#if DEBUG
    public const int MinimumLength = 1;
    public const bool RequiresComplexity = false;
#else
    public const int MinimumLength = 12;
    public const bool RequiresComplexity = true;
#endif

    public static string RegistrationDescription => RequiresComplexity
        ? "12–128 tegn med store og små bogstaver, et tal og et specialtegn."
        : "1–128 tegn. Simple adgangskoder er tilladt i Debug.";
}
