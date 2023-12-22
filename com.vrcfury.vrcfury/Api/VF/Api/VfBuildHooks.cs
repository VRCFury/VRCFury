using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace VF.Api {
    public static class VfBuildHooks {
        internal static List<(string,Action<GameObject>)> beforePlayModeBuildCallbacks
            = new List<(string,Action<GameObject>)>();

        public static void OnBeforePlayModeBuild(Action<GameObject> callback) {
            var callingClass = new StackTrace().GetFrame(1).GetMethod().DeclaringType;
            string source;
            if (callingClass != null) {
                source = $"{callingClass.Name} in {callingClass.Assembly.GetName().Name}";
            } else {
                source = "?";
            }

            beforePlayModeBuildCallbacks.Add((source,callback));
        }
    }
}
