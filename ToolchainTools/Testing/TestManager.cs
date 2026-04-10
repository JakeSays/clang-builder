using System.Collections.ObjectModel;
using System.Xml.Linq;
using Std.BuildTools.Clang.TestResources;

namespace Std.BuildTools.Clang.Testing;

internal static class TestManager
{
    public static ReadOnlyCollection<TestDefinition> Tests { get; } = ParseManifest();

    private static ReadOnlyCollection<TestDefinition> ParseManifest()
    {
        var xml = TestResourcesManager.GetTestManifest();
        if (xml == null)
        {
            return [];
        }

        var doc = XDocument.Parse(xml);

        return doc.Root!.Elements("Test")
            .Select(testEl => new TestDefinition(
                (string)testEl.Attribute("FileName")!,
                testEl.Elements("Skip")
                    .Select(s => new Skip(
                        ParseArch((string?)s.Attribute("Arch")),
                        ParseLibc((string?)s.Attribute("Libc")),
                        ParseType((string?)s.Attribute("Type")),
                        (string?)s.Attribute("Reason")))
                    .ToList()))
            .ToList()
            .AsReadOnly();
    }

    private static TargetArch? ParseArch(string? value) => value switch
    {
        "x64" => TargetArch.X64,
        "x32" => TargetArch.X86,
        "armv7" => TargetArch.Armv7,
        "aarch64" => TargetArch.Aarch64,
        "riscv64" => TargetArch.Riscv64,
        null => null,
        _ => throw new InvalidOperationException($"Unknown arch '{value}' in tests manifest")
    };

    private static TestLibc? ParseLibc(string? value) => value switch
    {
        "glibc" => TestLibc.Glibc,
        "musl" => TestLibc.Musl,
        null => null,
        _ => throw new InvalidOperationException($"Unknown libc '{value}' in tests manifest")
    };

    private static TestType? ParseType(string? value) => value switch
    {
        "static" => TestType.Static,
        "shared" => TestType.Shared,
        null => null,
        _ => throw new InvalidOperationException($"Unknown type '{value}' in tests manifest")
    };
}
