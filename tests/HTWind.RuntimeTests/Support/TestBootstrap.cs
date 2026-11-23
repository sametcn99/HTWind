using System.Runtime.CompilerServices;

namespace HTWind.RuntimeTests.Support;

/// <summary>Isolates test data without modifying the application's PowerShell deployment.</summary>
internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        TestPaths.Initialize();
    }
}
