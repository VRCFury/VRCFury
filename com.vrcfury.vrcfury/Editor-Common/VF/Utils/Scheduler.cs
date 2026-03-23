using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class Scheduler {
        private static readonly List<Scheduled> list = new List<Scheduled>();

        private class Scheduled {
            public Action task;
            public long frequencyMs;
            public double nextTrigger;
        }
        
        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.update += () => {
                var now = EditorApplication.timeSinceStartup;
                foreach (var t in list) {
                    if (t.nextTrigger < now) {
                        try {
                            t.task();
                        } catch (Exception e) {
                            Debug.LogException(e);
                        }
                        t.nextTrigger = now + ((double)t.frequencyMs / 1000);
                    }
                }
            };
        }

        public static void Schedule(Action callback, int frequencyMs) {
            list.Add(new Scheduled {
                task = callback,
                frequencyMs = frequencyMs
            });
        }
    }
}
