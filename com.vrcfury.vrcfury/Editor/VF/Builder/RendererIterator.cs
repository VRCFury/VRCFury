using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Inspector;

namespace VF.Builder {
    public static class RendererIterator {
        /**
         * Gets all the renderers + associated meshes on the avatar.
         * This could have been a simple iteration, but for some reason unity uses Mesh Filters for non-skinned meshes for some reason.
         */
        public static  ICollection<Tuple<Renderer, Mesh, Action<Mesh>>> GetRenderersWithMeshes(VFGameObject obj) {
            var output = new List<Tuple<Renderer, Mesh, Action<Mesh>>>();
            foreach (var renderer in obj.GetComponentsInSelfAndChildren<Renderer>()) {
                if (renderer is SkinnedMeshRenderer skin) {
                    if (skin.sharedMesh == null) continue;
                    output.Add(Tuple.Create(
                        renderer,
                        skin.sharedMesh,
                        (Action<Mesh>)(m => {
                            skin.sharedMesh = m;
                            VRCFuryEditorUtils.MarkDirty(skin);
                        })
                    ));
                } else if (renderer is MeshRenderer) {
                    var mesh = renderer.gameObject.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    output.Add(Tuple.Create(
                        renderer,
                        mesh,
                        (Action<Mesh>)(m => {
                            var filter = renderer.gameObject.GetComponent<MeshFilter>();
                            if (!filter) filter = renderer.gameObject.AddComponent<MeshFilter>();
                            filter.sharedMesh = m;
                            VRCFuryEditorUtils.MarkDirty(filter);
                        })
                    ));
                }
            }

            return output;
        }
    }
}
