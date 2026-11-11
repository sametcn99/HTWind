using System.IO;

namespace HTWind.Services.Application;

/// <summary>
///     Central resolver for every HTWind file location under the per-user data directory.
///     An explicit <c>basePath</c> (used by tests) takes precedence, then the
///     <c>HTWIND_DATA_DIR</c> environment variable (used by E2E-launched processes so their
///     data is fully isolated), and finally the standard per-user application data folder.
/// </summary>
public static class HTWindPathProvider
{
    public const string DataDirectoryEnvironmentVariable = "HTWIND_DATA_DIR";

    public static string GetRootDirectory(string? basePath = null)
    {
        if (!string.IsNullOrWhiteSpace(basePath))
        {
            return basePath;
        }

        var environmentValue = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return environmentValue;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HTWind"
        );
    }

    public static string GetStateFilePath(string? basePath = null)
    {
        return Path.Combine(GetRootDirectory(basePath), "widgets-state.json");
    }

    public static string GetWidgetsDirectory(string? basePath = null)
    {
        return Path.Combine(GetRootDirectory(basePath), "Widgets");
    }

    public static string GetWorkspaceProfilesFilePath(string? basePath = null)
    {
        return Path.Combine(GetRootDirectory(basePath), "workspace-profiles.json");
    }

    public static string GetTemplateDirectory(string? basePath = null)
    {
        return Path.Combine(GetRootDirectory(basePath), "BuiltInTemplates");
    }

    public static string GetPermissionsFilePath(string? basePath = null)
    {
        return Path.Combine(GetRootDirectory(basePath), "widget-permissions.json");
    }

    public static string GetDeveloperModeFilePath(string? basePath = null)
    {
        return Path.Combine(GetRootDirectory(basePath), "developer-mode.enabled");
    }

    public static string GetRiskConsentFilePath(string? basePath = null)
    {
        return Path.Combine(GetRootDirectory(basePath), "risk-consent.accepted");
    }

    public static string GetBuiltInWidgetsInitializationFilePath(string? basePath = null)
    {
        return Path.Combine(GetRootDirectory(basePath), "built-in-widgets.initialized");
    }

    public static string GetWebView2RootDirectory()
    {
        return Path.Combine(GetRootDirectory(), "WebView2");
    }

    public static string GetEditorFallbackDirectory()
    {
        return Path.Combine(GetRootDirectory(), "EditorFallback");
    }
}
