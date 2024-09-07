using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class FixAudioService {
        [VFAutowired] private readonly AvatarManager manager;
        
        [FeatureBuilderAction(FeatureOrder.FixAudio)]
        public void Apply() {
            if (!BuildTargetUtils.IsDesktop()) {
                foreach (var audio in manager.AvatarObject.GetComponentsInSelfAndChildren<AudioSource>()) {
                    Object.DestroyImmediate(audio);
                }
                foreach (var c in manager.GetAllUsedControllers()) {
                    foreach (var layer in c.GetLayers()) {
                        AnimatorIterator.ForEachBehaviourRW(layer, (b, add) => {
                            if (b is VRCAnimatorPlayAudio) return false;
                            return true;
                        });
                    }
                }
                return;
            }

            var cache = new Dictionary<AudioClip, AudioClip>();
            AudioClip FixClip(AudioClip input) {
                if (input == null) return null;
                if (cache.TryGetValue(input, out var cached)) return cached;
                AudioClip output;
                if (input.loadInBackground) {
                    output = input;
                } else {
                    output = input.Clone();
                    var so = new SerializedObject(output);
                    so.Update();
                    so.FindProperty("m_LoadInBackground").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                cache[input] = output;
                return output;
            }
            
            foreach (var audio in manager.AvatarObject.GetComponentsInSelfAndChildren<AudioSource>()) {
                var newClip = FixClip(audio.clip);
                if (newClip != audio.clip) {
                    audio.clip = newClip;
                    EditorUtility.SetDirty(audio);
                    if (audio.enabled) {
                        audio.enabled = false;
                        audio.enabled = true;
                    }
                }
            }
            foreach (var c in manager.GetAllUsedControllers()) {
                foreach (var b in new AnimatorIterator.Behaviours().From(c.GetRaw())) {
                    if (b is VRCAnimatorPlayAudio audio) {
                        audio.Clips = audio.Clips.Select(FixClip).ToArray();
                        EditorUtility.SetDirty(audio);
                    }
                }
            }
        }
    }
}
