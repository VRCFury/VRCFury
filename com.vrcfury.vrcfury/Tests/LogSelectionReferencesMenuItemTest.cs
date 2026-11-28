using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Menu;

namespace VF.Tests {
    internal class LogSelectionReferencesMenuItemTest {
        [TestCase(true)]
        [TestCase(false)]
        public void FindsOutsideReferencesInLoadedAndUnloadedScenes(bool incoming) {
            var testDir = AssetDatabase.GenerateUniqueAssetPath("Assets/Selection Usages Test");
            var selectedDir = testDir + "/Selected";
            var materialPath = selectedDir + "/Selected.mat";
            var scenePath = testDir + "/Closed.unity";
            GameObject selected = null;
            GameObject outside = null;
            try {
                AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(testDir));
                AssetDatabase.CreateFolder(testDir, "Selected");
                var material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);

                Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out var guid, out long id), Is.True);
                System.IO.File.WriteAllText(
                    scenePath,
                    "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!104 &2\nRenderSettings:\n"
                    + $"  m_SkyboxMaterial: {{fileID: {id}, guid: {guid}, type: 2}}\n"
                );
                AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);

                selected = new GameObject("Selected Root");
                var child = new GameObject("Selected Child");
                child.transform.SetParent(selected.transform);
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                outside = new GameObject("Outside LOD");
                outside.AddComponent<LODGroup>().SetLODs(new[] { new LOD(0.5f, new Renderer[] { renderer }) });

                var results = new List<string>();
                var sceneCount = SceneManager.sceneCount;
                var selection = incoming
                    ? new[] { AssetDatabase.LoadMainAssetAtPath(selectedDir), selected }
                    : new[] { AssetDatabase.LoadMainAssetAtPath(scenePath), outside };
                Assert.That(LogSelectionReferencesMenuItem.Scan(
                    selection,
                    (_, message) => results.Add(message)
                ), Is.True);
                Assert.That(results, Does.Contain(incoming ? $"{materialPath} <- {scenePath}" : $"{scenePath} -> {materialPath}"));
                Assert.That(results.Any(result => incoming
                    ? result.StartsWith("Selected Root/Selected Child <- ") && result.Contains("Outside LOD")
                    : result.Contains("Outside LOD") && result.EndsWith(" -> Selected Root/Selected Child")), Is.True);
                Assert.That(results.Any(result => result.Contains("(MeshRenderer.") && result.Contains(materialPath)), Is.False);
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
                Assert.That(SceneManager.GetSceneByPath(scenePath).isLoaded, Is.False);
            } finally {
                Object.DestroyImmediate(outside);
                Object.DestroyImmediate(selected);
                AssetDatabase.DeleteAsset(testDir);
            }
        }
    }
}
