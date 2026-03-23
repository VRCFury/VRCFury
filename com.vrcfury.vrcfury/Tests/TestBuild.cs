using NUnit.Framework;
using Mono.Cecil;
using System.IO;
using System.Linq;

[Category("VRCFury")]
public class TestBuild {
    [Test]
    public void Test() {
        var assemblyPath = Path.Combine("Library", "ScriptAssemblies", "VRCFury.dll");
        Assert.That(File.Exists(assemblyPath), Is.True, $"Missing assembly: {assemblyPath}");

        using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath)) {
            var assemblyRefs = assembly.MainModule.AssemblyReferences
                .Select(r => r.Name)
                .ToList();
            Assert.That(assemblyRefs, Does.Not.Contain("UnityEditor"));

            var badTypeRefs = assembly.MainModule.GetTypeReferences()
                .Where(t => t.Namespace == "UnityEditor" || t.Namespace.StartsWith("UnityEditor."))
                .Select(t => t.FullName)
                .Distinct()
                .ToList();
            Assert.That(
                badTypeRefs,
                Is.Empty,
                "Runtime assembly references UnityEditor types:\n" + string.Join("\n", badTypeRefs)
            );
        }
    }
}
