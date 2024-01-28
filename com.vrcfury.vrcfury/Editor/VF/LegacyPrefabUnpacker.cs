using System.IO;
using UnityEditor;
using UnityEngine;

namespace VF {
    [InitializeOnLoad]
    public static class LegacyPrefabUnpacker {
        static LegacyPrefabUnpacker() {
            ScanAlways();
        }

        public class PostProcessor : AssetPostprocessor {
#if UNITY_2021_3_OR_NEWER
            internal static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
#else
            internal static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
#endif
                ScanAlways();
            }
        }

        private static void ScanAlways() {
            // GogoLoco
            if (
                Exists("4485bd5c6aea29e48bf1428a18876fcc") // controller from the artist
                && !Exists("c8ff110e1741d344195bdc3174fa507f") // vrcfury prefab from this package
            ) {
                Import("3");
            }
        }
        
        public static void ScanOnce() {
            Debug.Log("VRCFury is scanning for needed legacy prefabs");
            ScanAlways();
            
            // Czarwolf Hybrid
            if (
                Exists("3bbf1d098ecdd084487b259b75a64251") // controller from the artist
                && !Exists("b3e4c8427fb49f84bad5f0048d1453c7") // vrcfury prefab from this package
            ) { 
                Import("1");
            }
            
            // Foxhole Bracelet
            if (
                Exists("2a76226ca69c2ab4cbe03615d966ff74") // controller from the artist
                && !Exists("0db0ce8550c4e744cad37cdb93c01a05") // vrcfury prefab from this package
                && !Exists("c8ef2bab2d017f8439d2ee7eb5664982") // new vrcfury prefab from the artist
            ) {
                Import("2");
            }

            // HeftyBits
            if (
                Exists("da0f7a25615533849a9c7c165e046df9") // controller from the artist
                && !Exists("e4ea69a2106516b499c04965f618bb49") // vrcfury prefab from the package
                && !Exists("4e3275f79a2c3f842820f540d56cebf1") // new vrcf prefab from the artist
            ) {
                Import("4");
            }
            
            // JMctrl Animated K9
            if (
                Exists("3de8d01c7927ec24ba20baf712557d2c") // controller from the artist
                && !Exists("6a4da46ae927b3747ba427753e121254") // vrcfury prefab from this package
            ) {
                Import("5");
            }
            
            // JMctrl Furry Anthro
            if (
                Exists("9bdf7c75e9f5df84dad098ec3dcecd25") // controller from the artist
                && !Exists("d1a51ee28d0f1214099b9fc69e1ef6ec") // vrcfury prefab from this package
            ) {
                Import("6");
            }
            
            // JMctrl Furry Freal
            if (
                Exists("9bdf7c75e9f5df84dad098ec3dcecd25") // controller from the artist
                && !Exists("24f78d8640050ee40b841d016a1cb8ed") // vrcfury prefab from this package
            ) {
                Import("7");
            }
            
            // JMctrl Furry Freal v2
            if (
                Exists("90b406ba9125b634c8b0cffe8fb8b796") // model from the artist
                && !Exists("599c5a8a719143c4aaa183caa113a0bd") // vrcfury prefab from this package
            ) {
                Import("8");
            }
            
            // Kara v1
            if (
                Exists("46ad84eb1e149554cba6f9d9b8dac925") // model from the artist
                && !Exists("249db48512adaa3409ba424509a2dea8") // vrcfury prefab from this package
            ) {
                Import("9");
            }
            
            // Kara v2
            if (
                Exists("721ed7ef0f75b634faa073dab98f979e") // model from the artist
                && !Exists("2f6bb48e4570c804f8c65a650f73ec40") // vrcfury prefab from this package
            ) {
                Import("10");
            }
            
            // Killer Kobold
            if (
                Exists("cf9362b41548fe24f91115c24c2cb522") // controller from the artist
                && !Exists("b11d52a88502b0c4cbdab33317605a01") // vrcfury prefab from this package
            ) {
                Import("11");
            }
            
            // Liindy Butterfly Knife
            if (
                Exists("bdb72eadd9ed318468934652f1d00cd5") // controller from the artist
                && !Exists("9d9b0766f89111c40af60d0db698a7d2") // vrcfury prefab from this package
            ) {
                Import("12");
            }
            
            // Liindy Death Sickles
            if (
                Exists("d04751d140fbe6e4e865183d78bd9677") // controller from the artist
                && !Exists("3dd3cbe8e7c6ea7408a4475e64e31781") // vrcfury prefab from this package
            ) {
                Import("13");
            }
            
            // Liindy Sniper
            if (
                Exists("051dccd7ccc0aba45b4ab114367b8315") // controller from the artist
                && !Exists("c1dfb5a228b158c48bf803b6d7e1d0a2") // vrcfury prefab from this package
            ) {
                Import("14");
            }
            
            // TFKS Scaling
            if (
                Exists("c414e1edae7f0c943bf647b14d2c1a11") // controller from the artist
                && !Exists("4c3104f7897d91e4e94c574506a691f4") // vrcfury prefab from this package
            ) {
                Import("15");
            }
            
            // ThiccWater
            if (
                Exists("585538695cedd4440a6b2a4e2a6e7803") // controller from the artist
                && !Exists("eff63cfcad0eb844d87d5ec61281fc14") // vrcfury prefab from this package
            ) {
                Import("16");
            }
            
            // Winterpaw Asset
            if (
                Exists("79c987e9aaa46d4428c2071632fd7dbe") // controller from the artist
                && !Exists("13d6bbe6d7fa9f740bfa3883e4e45f5a") // vrcfury prefab from this package
                && !Exists("404f2df34fa77da469e82101ebcd80cd") // new vrcfury prefab from the artist
            ) {
                Import("17");
            }
            
            // Zawoo Chaos Canine DLC1
            if (
                Exists("1a1800fd0b9941040bd43d78b00edb73") // model from the artist
                && !Exists("463c1f475ef4de54880595046ff816c2") // vrcfury prefab from this package
            ) {
                Import("18");
            }
            
            // Zawoo Daring Deer DLC1
            if (
                Exists("90edc659dc928b84f947cf47168e1538") // model from the artist
                && !Exists("a2982dae8495ba8419e5d2f579719403") // vrcfury prefab from this package
            ) {
                Import("19");
            }
            
            // Zawoo Hybrid Anthro
            if (
                Exists("0154f44a0736acf47b2f9c40f1e0fff9") // controller from the artist
                && !Exists("76c99ebb174cd63479c0674f4775e5a6") // vrcfury prefab from this package
            ) {
                Import("20");
            }
            
            // Zawoo Knotty Canine
            if (
                Exists("d7d4fa511891a7449bf6b1c73b0548c5") // controller from the artist
                && !Exists("4e123c337547c1d46acd84a73fb195b7") // vrcfury prefab from this package
            ) {
                Import("21");
            }
            
            // Zawoo Rascal Rabbit DLC1
            if (
                Exists("2c4016831a4a7fc4084aa6d77d67ce65") // model from the artist
                && !Exists("e09afe652b6817644aa605e210cceeba") // vrcfury prefab from this package
            ) {
                Import("22");
            }
            
            // Zawoo Rascal Rabbit DLC2
            if (
                Exists("edbcea764010e2142b78f5abd4d6a5e0") // model from the artist
                && !Exists("294b7c3175649274e9c23496e082626d") // vrcfury prefab from this package
            ) {
                Import("23");
            }
            
            // Zawoo Rascal Rabbit DLC3
            if (
                Exists("9ec0f638eab04d64f9b0d202652a3179") // model from the artist
                && !Exists("27225584ed8fcc544b21d9453ab3d368") // vrcfury prefab from this package
            ) {
                Import("24");
            }
        }

        private static bool Exists(string guid) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == null) return false;
            return File.Exists(path);
        }

        private static void Import(string id) {
            Debug.Log("VRCFury is importing legacy prefab #" + id);
            var resourcesPath = AssetDatabase.GUIDToAssetPath("c4e4fa889bc2bc54abfc219a5424b763");
            AssetDatabase.ImportPackage($"{resourcesPath}/LegacyPrefabPackages~/{id}.unitypackage", false);
        }
    }
}
