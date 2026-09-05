namespace RecipeItemCreator.Configuration;

internal static class AppSettings
{
    // Öffentliche GitHub-Repository-URL.
    // Beispiel:
    // https://github.com/DEIN-NAME/RecipeItemCreator
    //
    // Solange die URL leer ist, bleibt die Update-Prüfung deaktiviert.
    public const string GitHubRepositoryUrl = "";

    public static bool GitHubUpdatesConfigured =>
        TryGetGitHubRepository(out _, out _);

    public static bool TryGetGitHubRepository(
        out string owner,
        out string repository)
    {
        owner = string.Empty;
        repository = string.Empty;

        if (string.IsNullOrWhiteSpace(GitHubRepositoryUrl))
            return false;

        if (!Uri.TryCreate(
                GitHubRepositoryUrl.Trim(),
                UriKind.Absolute,
                out Uri? uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals(
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!uri.Host.Equals(
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] segments = uri.AbsolutePath
            .Trim('/')
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (segments.Length != 2)
            return false;

        owner = segments[0];

        repository = segments[1].EndsWith(
            ".git",
            StringComparison.OrdinalIgnoreCase)
                ? segments[1][..^4]
                : segments[1];

        return !string.IsNullOrWhiteSpace(owner) &&
               !string.IsNullOrWhiteSpace(repository);
    }
}