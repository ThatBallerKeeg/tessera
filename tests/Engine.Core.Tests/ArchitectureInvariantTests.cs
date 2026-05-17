using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using AwesomeAssertions;

namespace Engine.Core.Tests;

public sealed class ArchitectureInvariantTests
{
    [Fact]
    public void EngineCoreReferencesNoRenderingOrUiAssemblies()
    {
        string testDir = Path.GetDirectoryName(typeof(ArchitectureInvariantTests).Assembly.Location)!;
        string coreAssemblyPath = Path.Combine(testDir, "Engine.Core.dll");

        File.Exists(coreAssemblyPath).Should().BeTrue(
            $"Engine.Core.dll must be present in the test output directory: {coreAssemblyPath}");

        using var stream = File.OpenRead(coreAssemblyPath);
        using var peReader = new PEReader(stream);
        MetadataReader mr = peReader.GetMetadataReader();

        var offending = new List<string>();

        foreach (AssemblyReferenceHandle handle in mr.AssemblyReferences)
        {
            string name = mr.GetString(mr.GetAssemblyReference(handle).Name);
            if (name.StartsWith("MonoGame", StringComparison.Ordinal) ||
                name.StartsWith("Avalonia", StringComparison.Ordinal))
            {
                offending.Add(name);
            }
        }

        offending.Should().BeEmpty(
            $"Engine.Core must not reference MonoGame or Avalonia assemblies. Offending: {string.Join(", ", offending)}");
    }
}
