using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal sealed class VFTreeChild {
        public string directBlendParameter;
        public float threshold;
        public Vector2 position;
        public float timeScale = 1;
        public float cycleOffset;
        public bool mirror;
        public VFMotion motion;

        public static VFTreeChild Load(ChildMotion child, VFLoadContext context) {
            return new VFTreeChild {
                directBlendParameter = child.directBlendParameter,
                threshold = child.threshold,
                position = child.position,
                timeScale = child.timeScale,
                cycleOffset = child.cycleOffset,
                mirror = child.mirror,
                motion = VFMotion.Load(child.motion, context)
            };
        }

        public VFTreeChild ShallowClone() {
            return new VFTreeChild {
                directBlendParameter = directBlendParameter,
                threshold = threshold,
                position = position,
                timeScale = timeScale,
                cycleOffset = cycleOffset,
                mirror = mirror,
                motion = motion
            };
        }

        public VFTreeChild Clone(VFCloneContext context) {
            return new VFTreeChild {
                directBlendParameter = directBlendParameter,
                threshold = threshold,
                position = position,
                timeScale = timeScale,
                cycleOffset = cycleOffset,
                mirror = mirror,
                motion = motion?.Clone(context)
            };
        }
    }
}
