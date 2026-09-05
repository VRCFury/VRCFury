using System;
using System.Linq;
using System.Reflection;
using UnityEditor.Animations;

namespace VF.Utils {
    public static class AnimatorControllerToolHelper {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type AnimatorControllerTool = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimatorControllerTool");
            public static readonly PropertyInfo AnimatorControllerToolAnimatorController = AnimatorControllerTool?
                .VFProperty("animatorController");
        }

        public static AnimatorController GetPreviewedAnimatorController() {
            var tool = EditorWindowFinder.GetWindows(Reflection.AnimatorControllerTool).FirstOrDefault();
            if (tool == null) return null;
            return Reflection.AnimatorControllerToolAnimatorController.GetValue(tool) as AnimatorController;
        }
    }
}
