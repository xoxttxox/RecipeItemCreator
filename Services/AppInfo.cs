using System.Reflection;

namespace RecipeItemCreator.Services;

internal static class AppInfo
{
    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();

    public static Version CurrentVersion =>
        Assembly.GetName().Version ?? new Version(1, 0, 0, 0);

    public static string DisplayVersion
    {
        get
        {
            Version version = CurrentVersion;

            return version.Build >= 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : $"{version.Major}.{version.Minor}";
        }
    }

    public static string VersionText =>
        $"Version {DisplayVersion}";

    public static string ProductName =>
        Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
        ?? "Recipe Item Creator";

    public static string InformationalVersion =>
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? DisplayVersion;
}