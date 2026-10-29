using DotNetArch.Core.Scaffolding.Solution;
using Xunit;

namespace DotNetArch.Core.Tests;

public class MigrationFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dotnetarch-migfiles-" + Guid.NewGuid().ToString("N"));

    public MigrationFilesTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Write(string name, bool bom)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, [.. (bom ? new byte[] { 0xEF, 0xBB, 0xBF } : []), .. "using System;\n"u8.ToArray()]);
        return path;
    }

    private static bool HasBom(string path) => File.ReadAllBytes(path) is [0xEF, 0xBB, 0xBF, ..];

    [Fact]
    public void A_byte_order_mark_is_removed_when_the_editorconfig_asks_for_plain_utf8()
    {
        var withBom = Write("20261029_Init.cs", bom: true);
        var without = Write("20261029_Init.Designer.cs", bom: false);

        var changed = MigrationFiles.Normalize(_folder, "[*]\ncharset = utf-8\n");

        Assert.Equal([withBom], changed);
        Assert.False(HasBom(withBom));
        Assert.Equal("using System;\n", File.ReadAllText(withBom));
        Assert.False(HasBom(without));
    }

    [Fact]
    public void A_solution_that_asks_for_utf8_bom_keeps_the_mark()
    {
        var withBom = Write("20261029_Init.cs", bom: true);

        Assert.Empty(MigrationFiles.Normalize(_folder, "[*]\ncharset = utf-8-bom\n"));
        Assert.True(HasBom(withBom));
    }

    [Fact]
    public void A_missing_folder_or_editorconfig_is_not_an_error()
    {
        Assert.Empty(MigrationFiles.Normalize(Path.Combine(_folder, "nope"), null));
        var withBom = Write("a.cs", bom: true);

        MigrationFiles.Normalize(_folder, null);

        Assert.False(HasBom(withBom));
    }
}
