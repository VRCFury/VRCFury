using System;
using System.Linq;
using System.Reflection;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using VF.Utils;

namespace VF.Hooks {
    /**
     * When bakery is in a project, it has to apply its lightmap offsets to static-batched renderers before
     * unity collects them all and builds them into the "batched" super mesh.
     *
     * Usually this works properly, since that application happens in Awake of the ftLightmapsStorage,
     * however, when Reload Scene is disabled in play mode, Awake doesn't trigger at the proper time, which means
     * unity builds the super lightmap data using the wrong offsets, resulting in "corrupt" looking lightmaps
     * on those static batched renderers.
     *
     * This patch causes the ftLightmapsStorage to "awake" (again) during the play mode scene build, which IS
     * the right time, which resolves this issue.
     */
    internal class BakeryStaticBatchPlayModeFixHook : IProcessSceneWithReport {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type ftLightmapsStorage = ReflectionUtils.GetTypeFromAnyAssembly("ftLightmapsStorage");
            public static readonly MethodInfo ftLightmapsStorageAwake = ftLightmapsStorage?.VFMethod("Awake");
        }

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report) {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            foreach (var s in scene.Roots()
                         .SelectMany(r => r.GetComponentsInSelfAndChildren(Reflection.ftLightmapsStorage))) {
                Reflection.ftLightmapsStorageAwake.Invoke(s,  new object[] { });
            }
        }
    }
}
