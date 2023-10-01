using System;
using UnityEngine;
using VF.Upgradeable;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Model {
    // These are here because you can't use Serializable with generics until unity 2020
    // https://forum.unity.com/threads/generics-serialization.746300/

    [Serializable]
    public class GuidAnimationClip : GuidWrapper<AnimationClip> {
        public static implicit operator GuidAnimationClip(AnimationClip d) => new GuidAnimationClip {
            objOverride = d
        };
    }
    
    [Serializable]
    public class GuidMaterial : GuidWrapper<Material> {
        public static implicit operator GuidMaterial(Material d) => new GuidMaterial {
            objOverride = d
        };
    }
    
    [Serializable]
    public class GuidTexture2d : GuidWrapper<Texture2D> {
        public static implicit operator GuidTexture2d(Texture2D d) => new GuidTexture2d {
            objOverride = d
        };
    }
    
    [Serializable]
    public class GuidController : GuidWrapper<RuntimeAnimatorController> {
        public static implicit operator GuidController(RuntimeAnimatorController d) => new GuidController {
            objOverride = d
        };
    }
    
    [Serializable]
    public class GuidMenu : GuidWrapper<VRCExpressionsMenu> {
        public static implicit operator GuidMenu(VRCExpressionsMenu d) => new GuidMenu {
            objOverride = d
        };
    }
    
    [Serializable]
    public class GuidParams : GuidWrapper<VRCExpressionParameters> {
        public static implicit operator GuidParams(VRCExpressionParameters d) => new GuidParams {
            objOverride = d
        };
    }

    [Serializable]
    public class GuidWrapper<T> : GuidWrapper {
        // This field is only here for scripts to use temporarily. It's not saved.
        [NonSerialized] public T objOverride;
    }

    [Serializable]
    public class GuidWrapper : VrcfUpgradeable {
        [Obsolete] [SerializeField] private long fileID;
        [Obsolete] [SerializeField] private string guid;
        [SerializeField] public string id;
        [SerializeField] public Object objRef; // This is only here so that unity will export dependencies properly
        
#pragma warning disable 0612
        public override bool Upgrade(int fromVersion) {
            var changed = false;
            
            if (fromVersion < 1) {
                if (guid != "") id = guid + ":" + fileID;
            }

            if (UpdateIdIfPossible != null) {
                changed |= UpdateIdIfPossible(this);
            }

            return changed;
        }
#pragma warning restore 0612

        public static Func<GuidWrapper,bool> UpdateIdIfPossible;

        public override int GetLatestVersion() {
            return 1;
        }
    }
}
