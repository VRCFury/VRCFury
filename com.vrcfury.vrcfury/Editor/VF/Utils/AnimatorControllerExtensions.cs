using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Editor.VF.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    public static class AnimatorControllerExtensions {
        public static void Rewrite(this AnimatorController c, AnimationRewriter rewriter) {
            // Rewrite clips
            foreach (var clip in new AnimatorIterator.Clips().From(c)) {
                clip.Rewrite(rewriter);
            }

            // Rewrite masks
            foreach (var layer in c.layers) {
                var mask = layer.avatarMask;
                if (mask == null || mask.transformCount == 0) continue;
                mask.SetTransforms(mask.GetTransforms()
                    .Select(rewriter.RewritePath)
                    .Where(path => path != null));
            }
        }
    }
}
