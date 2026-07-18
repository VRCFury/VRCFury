using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /**
     * The VRCSDK requires that mipmap streaming must be enabled on textures if mipmaps are present.
     * For textures added using vrcfury, we just automatically fix this for the user.
     */
    [VFService]
    internal class FixMipmapStreamingService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction(FeatureOrder.FixMipmapStreaming)]
        public void Apply() {
            var texCache = new Dictionary<Texture2D, Texture2D>();
            Texture2D OptimizeTexture(Texture2D original) {
                if (texCache.TryGetValue(original, out var output)) return output;
                if (original.mipmapCount > 1 && !original.streamingMipmaps) {
                    output = original.Clone("Needed to enable mipmap streaming on texture to make VRCSDK happy");
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
                foreach (var propertyName in original.shader.GetPropertyNames(ShaderPropertyType.Texture)) {
                    if (!original.TryGetTextureFast(propertyName, out var texture)) continue;
                    var oldTexture = texture as Texture2D;
                    if (oldTexture == null) continue;
                    var newTexture = OptimizeTexture(oldTexture);
                    if (oldTexture != newTexture) {
                        output = output.Clone($"Needed to swap texture {oldTexture.name} to a copy that has mipmap streaming enabled");
                        output.SetTextureFast(propertyName, newTexture);
                    }
                }
                return matCache[original] = output;
            }

            foreach (var renderer in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(OptimizeMat).ToArray();
            }

            var clipRewriter = AnimationRewriter.RewriteObject(o => (o is Material m) ? OptimizeMat(m) : o);
            foreach (var controller in controllers.GetAllUsedControllers()) {
                controller.Rewrite(clipRewriter);
            }
        }
    }
}
