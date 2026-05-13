using System.Reflection;
using System.Text;


namespace Std.BuildTools.Clang.TestResources;

internal enum TestResourceType
{
	MainToolchain,
	BootstrapToolchain
}

internal static class TestResourcesManager
{
	private const string ResourcePrefix = "Std.BuildTools.Clang.TestResources.";
	private static readonly Assembly MyAssembly = typeof(TestResourcesManager).Assembly;

	public static string[] BootstrapTestNames { get; private set; }
	public static string[] MainTestNames { get; private set; }

	static TestResourcesManager()
	{
		var resourceNames = MyAssembly.GetManifestResourceNames()
			.Select(n => n.Replace(ResourcePrefix, ""))
			.ToArray();

		MainTestNames = resourceNames
			.Where(n => n.StartsWith("Main"))
			.Select(n => n[5..])
			.ToArray();
		BootstrapTestNames = resourceNames
			.Where(n => n.StartsWith("Bootstrap"))
			.Select(n => n[10..])
			.ToArray();
	}

	public static string? GetTestManifest() => ReadString($"{ResourcePrefix}Main.TestsManifest");

	private static string? ReadString(string resourceName)
	{
		using var stream = MyAssembly.GetManifestResourceStream(resourceName);
		if (stream == null)
		{
			return null;
		}

		using var reader = new StreamReader(stream, Encoding.UTF8);
		return reader.ReadToEnd();
	}

	public static string? GetTestSourceCode(TestResourceType type, string resourceName)
	{
		ArgumentException.ThrowIfNullOrEmpty(resourceName);

		resourceName =
			$"{ResourcePrefix}{(type == TestResourceType.MainToolchain ? "Main" : "Bootstrap")}.{resourceName}";

		return ReadString(resourceName);
	}
}
