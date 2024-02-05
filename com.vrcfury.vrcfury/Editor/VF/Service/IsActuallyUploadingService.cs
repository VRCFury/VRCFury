using System;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using VF.Injector;
using Debug = UnityEngine.Debug;

namespace VF.Service {
    [VFService]
    public class IsActuallyUploadingService {
        private readonly bool _cached;

        public IsActuallyUploadingService() {
            _cached = Determine();
            Debug.Log("We are" + (_cached ? "" : " NOT") + " actually uploading");
        }

        private bool Determine() {
            if (Application.isPlaying) return false;
            var stack = new StackTrace().GetFrames();
            if (stack == null) return true;
            var preprocessFrame = stack
                .Select((frame, i) => (frame, i))
                .Where(f => f.frame.GetMethod().Name == "OnPreprocessAvatar" &&
                            (f.frame.GetMethod().DeclaringType?.FullName ?? "").StartsWith("VRC."))
                .Select(pair => pair.i)
                .DefaultIfEmpty(-1)
                .Last();
            if (preprocessFrame < 0) return false; // Not called through preprocessor hook
            if (preprocessFrame >= stack.Length - 1) return true;

            var callingClass = stack[preprocessFrame + 1].GetMethod().DeclaringType?.FullName;
            if (callingClass == null) return true;
            Debug.Log("Build was invoked by " + callingClass);
            return callingClass.StartsWith("VRC.");
        }

        public bool Get() {
            return _cached;
        }
    }
}
