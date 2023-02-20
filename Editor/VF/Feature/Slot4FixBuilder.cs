using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class Slot4FixBuilder : FeatureBuilder<Slot4Fix> {
        public override string GetEditorTitle() {
            return "Slot 4 Fix";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "A unity bug typically prevents you from animating material slot 4. If you attempt to do so," +
                " it will corrupt the material in slot 2. Why? Who knows. This feature will detect and fix this issue" +
                " by rewriting affected meshes so that slot 4 material is moved to another slot."
            ));
            return content;
        }

        public override bool AvailableOnProps() {
            return false;
        }
        
        [FeatureBuilderAction(FeatureOrder.Slot4Fix)]
        public void Apply() {
            var meshesToPatch = new HashSet<Mesh>();

            foreach (var c in manager.GetAllUsedControllers()) {
                c.ForEachClip(clip => {
                    foreach (var binding in clip.GetObjectBindings()) {
                        if (binding.propertyName != "m_Materials.Array.data[4]") continue;
                        var target = avatarObject.transform.Find(binding.path);
                        if (!target) continue;

                        Mesh mesh = null;
                        if (binding.type == typeof(SkinnedMeshRenderer)) {
                            var skin = target.GetComponent<SkinnedMeshRenderer>();
                            if (!skin) continue;
                            mesh = skin.sharedMesh;
                        } else if (binding.type == typeof(MeshRenderer)) {
                            var renderer = target.GetComponent<MeshFilter>();
                            if (!renderer) continue;
                            mesh = renderer.sharedMesh;
                        }

                        if (!mesh) continue;
                        var matCount = mesh.subMeshCount;
                        if (matCount < 5) continue;

                        meshesToPatch.Add(mesh);

                        var mutable = clip.GetMutable();
                        var curve = mutable.GetObjectCurve(binding);
                        mutable.SetObjectCurve(binding, null);
                        var newBinding = binding;
                        newBinding.propertyName = $"m_Materials.Array.data[{matCount}]";
                        mutable.SetObjectCurve(newBinding, curve);
                    }
                });
            }

            foreach (var mesh in meshesToPatch) {
                var meshCopy = Object.Instantiate(mesh);
                VRCFuryAssetDatabase.SaveAsset(meshCopy, tmpDir, "s4fix_" + meshCopy.name);
                var submesh = meshCopy.GetSubMesh(4);
                meshCopy.SetSubMesh(4, new SubMeshDescriptor());
                meshCopy.subMeshCount++;
                meshCopy.SetSubMesh(meshCopy.subMeshCount - 1, submesh);
                EditorUtility.SetDirty(meshCopy);

                foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                    if (skin.sharedMesh == mesh) {
                        skin.sharedMesh = meshCopy;
                        var mats = skin.sharedMaterials.ToList();
                        while (mats.Count < meshCopy.subMeshCount) mats.Add(null);
                        mats[meshCopy.subMeshCount - 1] = mats[4];
                        mats[4] = null;
                        skin.sharedMaterials = mats.ToArray();
                        EditorUtility.SetDirty(skin);
                    }
                }
                foreach (var skin in avatarObject.GetComponentsInChildren<MeshFilter>(true)) {
                    if (skin.sharedMesh == mesh) {
                        skin.sharedMesh = meshCopy;
                        EditorUtility.SetDirty(skin);
                    }

                    var renderer = skin.GetComponent<MeshRenderer>();
                    if (renderer) {
                        var mats = renderer.sharedMaterials.ToList();
                        while (mats.Count < meshCopy.subMeshCount) mats.Add(null);
                        mats[meshCopy.subMeshCount - 1] = mats[4];
                        mats[4] = null;
                        renderer.sharedMaterials = mats.ToArray();
                        EditorUtility.SetDirty(renderer);
                    }
                }
            }
        }
    }
}
