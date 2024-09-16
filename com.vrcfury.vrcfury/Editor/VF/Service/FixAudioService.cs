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
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        
        [FeatureBuilderAction(FeatureOrder.FixAudio)]
        public void Apply() {
            if (!BuildTargetUtils.IsDesktop()) {
                foreach (var audio in avatarObject.GetComponentsInSelfAndChildren<AudioSource>()) {
                    Object.DestroyImmediate(audio);
                }
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
                foreach (var c in controllers.GetAllUsedControllers()) {
                    foreach (var layer in c.GetLayers()) {
                        AnimatorIterator.ForEachBehaviourRW(layer, (b, add) => {
                            if (b is VRCAnimatorPlayAudio) return false;
                            return true;
                        });
                    }
                }
#endif
                return;
            }

            var cache = new Dictionary<AudioClip, AudioClip>();
            AudioClip FixClip(AudioClip input) {
                if (input == null) return null;
                if (cache.TryGetValue(input, out var cached)) return cached;
                AudioClip output;
                if (input.loadInBackground || input.loadType != AudioClipLoadType.DecompressOnLoad) {
                    output = input;
                } else {
                    output = input.Clone("Needed to enable Load In Background to make VRCSDK happy");
                    var so = new SerializedObject(output);
                    so.Update();
                    so.FindProperty("m_LoadInBackground").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                cache[input] = output;
                return output;
            }
            
            foreach (var audio in avatarObject.GetComponentsInSelfAndChildren<AudioSource>()) {
                var newClip = FixClip(audio.clip);
                if (newClip != audio.clip) {
                    var wasEnabled = audio.enabled;
                    audio.enabled = false;
                    audio.clip = newClip;
                    EditorUtility.SetDirty(audio);
                    audio.enabled = wasEnabled;
                }
            }
#if VRCSDK_HAS_ANIMATOR_PLAY_AUDIO
            foreach (var c in controllers.GetAllUsedControllers()) {
                foreach (var b in new AnimatorIterator.Behaviours().From(c.GetRaw())) {
                    if (b is VRCAnimatorPlayAudio audio) {
                        audio.Clips = audio.Clips.Select(FixClip).ToArray();
                        EditorUtility.SetDirty(audio);
                    }
                }
            }
#endif
        }
    }
}
