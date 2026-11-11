using Microsoft.Web.WebView2.Core;

namespace HTWind.RuntimeTests.Support;

/// <summary>Detects whether the machine has an installed, usable WebView2 Runtime.</summary>
public static class WebView2Runtime
{
    public static readonly bool IsAvailable = Probe();

    private static bool Probe()
    {
        try
        {
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
