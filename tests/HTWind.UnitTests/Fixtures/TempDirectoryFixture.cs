using System.IO;

namespace HTWind.UnitTests.Fixtures;

/// <summary>
///     Creates a unique temporary directory per test class (used via IClassFixture) and removes
///     it on dispose. All disk-touching unit tests run inside it so real user data is never
///     read or written.
/// </summary>
public sealed class TempDirectoryFixture : IDisposable
{
    public TempDirectoryFixture()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "htwind-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Sub-directory inside the fixture, created on demand.</summary>
    public string CreateSubdirectory(string name)
    {
        var fullPath = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public string CreateFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; leftover temp folders are harmless.
        }
    }
}
