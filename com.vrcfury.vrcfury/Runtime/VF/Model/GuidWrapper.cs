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
            setter = d
        };
    }
    
    [Serializable]
    public class GuidMaterial : GuidWrapper<Material> {
        public static implicit operator GuidMaterial(Material d) => new GuidMaterial {
            setter = d
        };
    }
    
    [Serializable]
    public class GuidTexture2d : GuidWrapper<Texture2D> {
        public static implicit operator GuidTexture2d(Texture2D d) => new GuidTexture2d {
            setter = d
        };
    }
    
    [Serializable]
    public class GuidController : GuidWrapper<RuntimeAnimatorController> {
        public static implicit operator GuidController(RuntimeAnimatorController d) => new GuidController {
            setter = d
        };
    }
    
    [Serializable]
    public class GuidMenu : GuidWrapper<VRCExpressionsMenu> {
        public static implicit operator GuidMenu(VRCExpressionsMenu d) => new GuidMenu {
            setter = d
        };
    }
    
    [Serializable]
    public class GuidParams : GuidWrapper<VRCExpressionParameters> {
        public static implicit operator GuidParams(VRCExpressionParameters d) => new GuidParams {
            setter = d
        };
    }

    [Serializable]
    public class GuidWrapper<T> : GuidWrapper where T : Object {
        // This field is only here for scripts to use temporarily. It's not saved.
        [NonSerialized] public T typeDetector;

        protected T setter {
            set {
                objRef = value;
                id = "";
                Sync();
            }
        }
        
#pragma warning disable 0612
        public override bool Upgrade(int fromVersion) {
            var changed = false;
            
            if (fromVersion < 1) {
                if (guid != "") id = guid + ":" + fileID;
            }

            changed |= Sync();

            return changed;
        }
#pragma warning restore 0612

        // We sync in preserialize to ensure that if an asset "reappeared" recently, and objRef is still null,
        // that this asset will still show up in the export dialog when the user exports their prefab
        protected override void PreSerialize() {
            Sync();
        }

        private bool Sync() {
            return SyncExt != null && SyncExt(this, typeof(T));
        }
    }

    [Serializable]
    public class GuidWrapper : VrcfUpgradeable {
        [Obsolete] [SerializeField] protected long fileID;
        [Obsolete] [SerializeField] protected string guid;
        [SerializeField] public string id;
        [SerializeField] public Object objRef;
        
        public static Func<GuidWrapper,Type,bool> SyncExt;

        public override int GetLatestVersion() {
            return 1;
        }
    }
}
