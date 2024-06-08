using System;
using UnityEngine;

namespace VF.Utils {
    internal static class Vector2Extensions {
        public static float Min(this Vector2 v) {
            return Math.Min(v.x, v.y);
        }
        public static float Max(this Vector2 v) {
            return Math.Max(v.x, v.y);
        }
    }
}
