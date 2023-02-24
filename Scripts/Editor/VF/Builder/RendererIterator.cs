using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VF.Builder {
    public static class RendererIterator {
        /**
         * Gets all the renderers + associated meshes on the avatar.
         * This could have been a simple iteration, but for some reason unity uses Mesh Filters for non-skinned meshes for some reason.
         */
        public static  ICollection<Tuple<Renderer, Mesh, Action<Mesh>>> GetRenderersWithMeshes(GameObject obj) {
            var output = new List<Tuple<Renderer, Mesh, Action<Mesh>>>();
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true)) {
                if (renderer is SkinnedMeshRenderer skin) {
                    output.Add(Tuple.Create(
                        renderer,
                        skin.sharedMesh,
                        (Action<Mesh>)(m => {
                            skin.sharedMesh = m;
                            EditorUtility.SetDirty(skin);
                        })
                    ));
                } else if (renderer is MeshRenderer) {
                    output.Add(Tuple.Create(
                        renderer,
                        renderer.gameObject.GetComponent<MeshFilter>()?.sharedMesh,
                        (Action<Mesh>)(m => {
                            var filter = renderer.gameObject.GetComponent<MeshFilter>();
                            if (!filter) filter = renderer.gameObject.AddComponent<MeshFilter>();
                            filter.sharedMesh = m;
                            EditorUtility.SetDirty(filter);
                        })
                    ));
                }
            }

            return output;
        }
    }
}
