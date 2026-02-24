using System.Globalization;
using System.Reflection;
using System.Resources;

namespace HTWind.Localization;

public static class LocalizationService
{
    private static readonly ResourceManager ResourceManager = new(
        "HTWind.Resources.Strings",
        Assembly.GetExecutingAssembly()
    );

    public static CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public static void SetCulture(string cultureName)
    {
        CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
    }

    public static string Get(string key)
    {
        return ResourceManager.GetString(key, CurrentCulture) ?? key;
    }
}
