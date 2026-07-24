using System;
using UnityEngine;
using VF.Exceptions;
using VF.Hooks.UnityFixes;

namespace VF.Utils {
    internal class VRCFuryBuildContext : IDisposable {
        private readonly IDisposable materialPropertyDrawers;
        private readonly IDisposable assetPostprocessors;
        private readonly IDisposable assetEditing;
        private bool disposed;

        public VRCFuryBuildContext() {
            materialPropertyDrawers = SuppressMaterialPropertyDrawersHook.Suppress();
            assetPostprocessors = SkipAssetPostprocessorsForVrcfAssetWritesHook.Suppress();
            assetEditing = VRCFuryAssetDatabase.WithAssetEditing();

            // If we don't do this, a unity issue in RepaintImmediately can randomly throw a segfault
            RenderTexture.active = null;
            Camera.SetupCurrent(null);
        }

        public static bool Run(Action action) {
            return VRCFExceptionUtils.ErrorDialogBoundary(() => {
                using (new VRCFuryBuildContext()) {
                    action();
                }
            });
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            try {
                assetEditing.Dispose();
            } finally {
                try {
                    assetPostprocessors.Dispose();
                } finally {
                    materialPropertyDrawers.Dispose();
                }
            }
        }
    }
}
