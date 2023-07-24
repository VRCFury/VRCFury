using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

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

                        var curve = clip.GetObjectCurve(binding);
                        clip.SetObjectCurve(binding, null);
                        var newBinding = binding;
                        newBinding.propertyName = $"m_Materials.Array.data[{matCount}]";
                        clip.SetObjectCurve(newBinding, curve);
                    }
                });
            }

            foreach (var oldMesh in meshesToPatch) {
                var newMesh = mutableManager.MakeMutable(oldMesh);
                var submesh = newMesh.GetSubMesh(4);
                newMesh.SetSubMesh(4, new SubMeshDescriptor());
                newMesh.subMeshCount++;
                newMesh.SetSubMesh(newMesh.subMeshCount - 1, submesh);
                VRCFuryEditorUtils.MarkDirty(newMesh);

                foreach (var tuple in RendererIterator.GetRenderersWithMeshes(avatarObject)) {
                    var (renderer, mesh, setMesh) = tuple;
                    if (mesh != oldMesh) continue;
                    setMesh(newMesh);
                    var mats = renderer.sharedMaterials.ToList();
                    while (mats.Count < newMesh.subMeshCount) mats.Add(null);
                    mats[newMesh.subMeshCount - 1] = mats[4];
                    mats[4] = null;
                    renderer.sharedMaterials = mats.ToArray();
                    VRCFuryEditorUtils.MarkDirty(renderer);
                }
            }
        }
    }
}
