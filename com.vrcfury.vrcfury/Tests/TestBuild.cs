using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Model;

public class TestBuild {
    [Test]
    public void Test() {
        var gameObject = new GameObject("test avatar");
        gameObject.AddComponent<VRCFuryTest>();
        var str2 = "Assets/test.prefab";
        PrefabUtility.SaveAsPrefabAsset(gameObject, str2);
        var assetBundleBuild = new AssetBundleBuild {
            assetNames = new []{ str2 },
            assetBundleName = "test.unity3d"
        };
        BuildPipeline.BuildAssetBundles(Application.temporaryCachePath, new AssetBundleBuild[1] {
            assetBundleBuild
        }, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
    }
}
