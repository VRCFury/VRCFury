using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /**
     * The VRCSDK requires that mipmap streaming must be enabled on textures if mipmaps are present.
     * For textures added using vrcfury, we just automatically fix this for the user.
     */
    [VFService]
    internal class FixMipmapStreaming {
        [VFAutowired] private readonly AvatarManager manager;

        [FeatureBuilderAction(FeatureOrder.FixMipmapStreaming)]
        public void Apply() {
            var texCache = new Dictionary<Texture2D, Texture2D>();
            Texture2D OptimizeTexture(Texture2D original) {
                if (original == null) return null;
                if (texCache.TryGetValue(original, out var output)) return output;
                if (original.mipmapCount > 1 && !original.streamingMipmaps) {
                    Debug.LogWarning($"VRCFury is enabling mipmap streaming on texture {original.name}");
                    output = original.Clone();
                    var so = new SerializedObject(output);
                    so.Update();
                    so.FindProperty("m_StreamingMipmaps").boolValue = true;
                    so.FindProperty("m_StreamingMipmapsPriority").intValue = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                } else {
                    output = original;
                }
                return texCache[original] = output;
            }

            var matCache = new Dictionary<Material, Material>();
            Material OptimizeMat(Material original) {
                if (original == null) return null;
                if (matCache.TryGetValue(original, out var output)) return output;
                output = original;
                foreach (var id in original.GetTexturePropertyNameIDs()) {
                    var oldTexture = original.GetTexture(id) as Texture2D;
                    var newTexture = OptimizeTexture(oldTexture);
                    if (oldTexture != newTexture) {
                        output = original.Clone();
                        output.SetTexture(id, newTexture);
                    }
                }
                return matCache[original] = output;
            }

            foreach (var renderer in manager.AvatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(OptimizeMat).ToArray();
            }

            var clipRewriter = AnimationRewriter.RewriteObject(o => (o is Material m) ? OptimizeMat(m) : o);
            foreach (var clip in manager.GetAllUsedControllers().SelectMany(c => c.GetClips())) {
                clip.Rewrite(clipRewriter);
            }
        }
    }
}
