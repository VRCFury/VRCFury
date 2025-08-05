using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.PlayMode {
    internal static class FixWriteDefaultsLater {
        private static readonly List<Action> onEditMode = new List<Action>();

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredEditMode) {
                    try {
                        foreach (var c in onEditMode) {
                            c();
                        }
                    } finally {
                        onEditMode.Clear();
                    }
                }
            };
        }

        public static void Save(VFGameObject avatar, bool auto) {
            if (Application.isPlaying) {
                onEditMode.Add(() => {
                    SaveNow(avatar, auto);
                });
            } else {
                SaveNow(avatar, auto);
            }
        }

        private static void SaveNow(VFGameObject avatar, bool auto) {
            if (avatar == null) {
                return;
            }

            if (avatar.GetComponentsInSelfAndChildren<VRCFury>()
                .SelectMany(v => v.GetAllFeatures())
                .Any(f => f is FixWriteDefaults)) {
                return;
            }

            var vf = avatar.AddComponent<VRCFury>();
            vf.content = new FixWriteDefaults() {
                mode = auto
                    ? FixWriteDefaults.FixWriteDefaultsMode.Auto
                    : FixWriteDefaults.FixWriteDefaultsMode.Disabled
            };
        }
    }
}
