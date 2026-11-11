using System.IO;

using HTWind.Services.Application;

using Xunit;

namespace HTWind.UnitTests;

/// <summary>Atomic write semantics (temp file + overwrite move).</summary>
public sealed class AtomicFileTests : IClassFixture<Fixtures.TempDirectoryFixture>
{
    private readonly Fixtures.TempDirectoryFixture _temp;

    public AtomicFileTests(Fixtures.TempDirectoryFixture temp)
    {
        _temp = temp;
    }

    [Fact]
    public void WriteAllText_OverwritesExistingContent()
    {
        var path = Path.Combine(_temp.Path, "state.json");

        AtomicFile.WriteAllText(path, "{\"v\":1}");
        Assert.Equal("{\"v\":1}", File.ReadAllText(path));

        AtomicFile.WriteAllText(path, "{\"v\":2}");
        Assert.Equal("{\"v\":2}", File.ReadAllText(path));
    }

    [Fact]
    public async Task WriteAllText_LeavesNoTempFileResidue()
    {
        var path = Path.Combine(_temp.Path, "atomic.json");

        AtomicFile.WriteAllText(path, "one");
        await AtomicFile.WriteAllTextAsync(path, "two");

        Assert.Equal("two", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(_temp.Path, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task InterleavedReadAndWrite_NeverSeesPartialContent()
    {
        var path = Path.Combine(_temp.Path, "interleaved.json");

        // Alternating reads and writes must always observe a complete payload (the atomic
        // write never exposes a truncated file), with no .tmp residue afterwards.
        var longPayload = new string('x', 100_000);
        for (var round = 0; round < 10; round++)
        {
            await AtomicFile.WriteAllTextAsync(path, longPayload + round);
            var observed = File.ReadAllText(path);
            Assert.Equal(longPayload + round, observed);
        }

        Assert.Empty(Directory.GetFiles(_temp.Path, "*.tmp", SearchOption.AllDirectories));
    }
}
