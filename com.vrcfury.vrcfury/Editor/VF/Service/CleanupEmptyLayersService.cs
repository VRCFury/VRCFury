using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /**
     * This builder removes animation bindings that do nothing, for cleanliness, to save space,
     * and to avoid including assets (like materials) that are referenced in animations but not actually used
     */
    [VFService]
    internal class CleanupEmptyLayersService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ValidateBindingsService validateBindingsService;
        
        [FeatureBuilderAction(FeatureOrder.CleanupEmptyLayers)]
        public void Apply() {
            foreach (var c in controllers.GetAllUsedControllers()) {
                var removedBindings = new List<string>();

                // Delete bindings targeting things that don't exist
                foreach (var clip in new AnimatorIterator.Clips().From(c)) {
                    if (clip.GetUseOriginalUserClip() != null && validateBindingsService.HasValidBinding(clip)) {
                        // We haven't touched this clip at all during the build so far,
                        // and it contains some useful bindings, so go ahead and just leave it as is
                        // so the original file will be used, rather than generating a fresh copy.
                    } else {
                        // Rip out all impossible bindings
                        clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                            if (!validateBindingsService.IsValid(binding)) {
                                removedBindings.Add($"{binding.PrettyString()} from {clip.name}");
                                return null;
                            }
                            return binding;
                        }));
                    }
                }

                if (removedBindings.Count > 0) {
                    Debug.LogWarning(
                        $"Removed {removedBindings.Count} properties from animation clips that targeted objects that do not exist:\n" +
                        removedBindings.Join('\n'));
                }

                // Delete empty layers
                foreach (var (layer, i) in c.GetLayers().Select((l,i) => (l,i))) {
                    var hasNonEmptyClip = new AnimatorIterator.Clips().From(layer)
                        .Any(clip => validateBindingsService.HasValidBinding(clip));
                    var hasBehaviour = layer.allBehaviours.Any();

                    if (!hasNonEmptyClip && !hasBehaviour) {
                        Debug.LogWarning($"Removing layer {layer.name} from {c.GetType()} because it doesn't do anything");
                        if (layer.hasDefaultState && !IsActuallyUploadingHook.Get()) {
                            layer.name += " (NO VALID ANIMATIONS)";
                            var s = layer.NewState(
                                "Warning from VRCFury!\n" +
                                "This layer contains no valid animations," +
                                " and will be removed during a real upload," +
                                " Make sure the animated objects / components" +
                                " actually exist at the paths used in the clips");
                            var entryPos = layer.entryPosition;
                            var warningPos = new Vector2(entryPos.x, entryPos.y - 100);
                            s.SetRawPosition(warningPos);
                        } else {
                            layer.Remove();
                        }
                    } else if (!hasNonEmptyClip) {
                        layer.weight = 0;
                    }
                }
            }
        }
    }
}
