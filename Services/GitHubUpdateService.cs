using System.Net.Http.Headers;
using System.Text.Json;
using RecipeItemCreator.Configuration;

namespace RecipeItemCreator.Services;

internal static class GitHubUpdateService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static async Task<GitHubUpdateResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (!AppSettings.TryGetGitHubRepository(out string owner, out string repository))
            return GitHubUpdateResult.NotConfigured();

        string requestUrl =
            $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/" +
            $"{Uri.EscapeDataString(repository)}/releases/latest";

        try
        {
            using HttpResponseMessage response =
                await HttpClient.GetAsync(requestUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return GitHubUpdateResult.Failed(
                    $"GitHub HTTP {(int)response.StatusCode} ({response.ReasonPhrase})");
            }

            await using Stream responseStream =
                await response.Content.ReadAsStreamAsync(cancellationToken);

            using JsonDocument document =
                await JsonDocument.ParseAsync(
                    responseStream,
                    cancellationToken: cancellationToken);

            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("tag_name", out JsonElement tagElement))
            {
                return GitHubUpdateResult.Failed(
                    "The GitHub release does not contain a version tag.");
            }

            string tag = tagElement.GetString()?.Trim() ?? string.Empty;

            if (!TryParseVersion(tag, out Version? latestVersion) ||
                latestVersion is null)
            {
                return GitHubUpdateResult.Failed(
                    $"The version tag '{tag}' could not be read.");
            }

            string releaseUrl =
                root.TryGetProperty("html_url", out JsonElement urlElement)
                    ? urlElement.GetString() ?? string.Empty
                    : string.Empty;

            Version normalizedCurrent = NormalizeVersion(currentVersion);
            Version normalizedLatest = NormalizeVersion(latestVersion);

            bool updateAvailable =
                normalizedLatest.CompareTo(normalizedCurrent) > 0;

            return updateAvailable
                ? GitHubUpdateResult.UpdateAvailable(
                    tag,
                    normalizedLatest,
                    releaseUrl)
                : GitHubUpdateResult.UpToDate(
                    tag,
                    normalizedLatest,
                    releaseUrl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return GitHubUpdateResult.Failed(
                "Timeout while checking for updates.");
        }
        catch (HttpRequestException ex)
        {
            return GitHubUpdateResult.Failed(
                $"GitHub could not be reached: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return GitHubUpdateResult.Failed(
                $"Invalid response from GitHub: {ex.Message}");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(
                "RecipeItemCreator",
                AppInfo.DisplayVersion));

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));

        client.DefaultRequestHeaders.Add(
            "X-GitHub-Api-Version",
            "2022-11-28");

        return client;
    }

    private static bool TryParseVersion(
        string tag,
        out Version? version)
    {
        string normalized = tag.Trim();

        if (normalized.StartsWith('v') ||
            normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        int suffixIndex = normalized.IndexOfAny(['-', '+']);

        if (suffixIndex >= 0)
            normalized = normalized[..suffixIndex];

        return Version.TryParse(normalized, out version);
    }

    private static Version NormalizeVersion(Version version)
    {
        return new Version(
            version.Major,
            version.Minor,
            Math.Max(version.Build, 0));
    }
}

internal enum GitHubUpdateState
{
    NotConfigured,
    UpToDate,
    UpdateAvailable,
    Failed
}

internal sealed record GitHubUpdateResult(
    GitHubUpdateState State,
    string Tag,
    Version? Version,
    string ReleaseUrl,
    string ErrorMessage)
{
    public static GitHubUpdateResult NotConfigured() =>
        new(
            GitHubUpdateState.NotConfigured,
            string.Empty,
            null,
            string.Empty,
            string.Empty);

    public static GitHubUpdateResult UpToDate(
        string tag,
        Version version,
        string releaseUrl) =>
        new(
            GitHubUpdateState.UpToDate,
            tag,
            version,
            releaseUrl,
            string.Empty);

    public static GitHubUpdateResult UpdateAvailable(
        string tag,
        Version version,
        string releaseUrl) =>
        new(
            GitHubUpdateState.UpdateAvailable,
            tag,
            version,
            releaseUrl,
            string.Empty);

    public static GitHubUpdateResult Failed(
        string errorMessage) =>
        new(
            GitHubUpdateState.Failed,
            string.Empty,
            null,
            string.Empty,
            errorMessage);
}