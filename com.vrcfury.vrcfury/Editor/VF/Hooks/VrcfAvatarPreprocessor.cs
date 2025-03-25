using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal abstract class VrcfAvatarPreprocessor : IVRCSDKPreprocessAvatarCallback {
        private static readonly Dictionary<GameObject,int> runsForObject = new Dictionary<GameObject,int>();

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingPlayMode) {
                    runsForObject.Clear();
                }
            };
        }

        internal class StartCheck : IVRCSDKPreprocessAvatarCallback {
            public int callbackOrder => int.MinValue;
            public bool OnPreprocessAvatar(GameObject obj) {
                runsForObject[obj] = GetRuns(obj) + 1;
                return true;
            }
        }

        public static int GetRuns(GameObject obj) {
            return runsForObject.TryGetValue(obj, out var runs) ? runs : 0;
        }

        // We don't run anything on MinValue because that's when StartCheck runs
        public int callbackOrder => order == int.MinValue ? int.MinValue + 1 : order;
        public bool OnPreprocessAvatar(GameObject obj) {
            // This is only here just in case the RunPreprocessorsOnlyOncePatch harmony patch didn't work (user running on a platform that doesn't support harmony)
            if (GetRuns(obj) > 1) {
                Debug.LogWarning("Skipping " + GetType().FullName + " preprocessor because it already ran on this object");
                return true;
            }
            return VRCFExceptionUtils.ErrorDialogBoundary(() => Process(obj));
        }

        protected abstract int order { get; }
        protected abstract void Process(VFGameObject obj);
    }
}
