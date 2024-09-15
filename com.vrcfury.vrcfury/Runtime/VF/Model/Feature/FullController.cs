using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VF.Model.Feature {
    [Serializable]
    internal class FullController : NewFeatureModel {
        public List<ControllerEntry> controllers = new List<ControllerEntry>();
        public List<MenuEntry> menus = new List<MenuEntry>();
        public List<ParamsEntry> prms = new List<ParamsEntry>();
        public List<SmoothParamEntry> smoothedPrms = new List<SmoothParamEntry>();
        public List<string> globalParams = new List<string>();
        public bool allNonsyncedAreGlobal = false;
        public bool ignoreSaved;
        public string toggleParam;
        public GameObject rootObjOverride;
        public bool rootBindingsApplyToAvatar;
        public List<BindingRewrite> rewriteBindings = new List<BindingRewrite>();
        public bool allowMissingAssets = false;
        public string injectSpsDepthParam;

        [Obsolete] public GuidController controller;
        [Obsolete] public GuidMenu menu;
        [Obsolete] public GuidParams parameters;
        [Obsolete] public string submenu;
        [Obsolete] public List<string> removePrefixes = new List<string>();
        [Obsolete] public string addPrefix = "";
        [Obsolete] public bool useSecurityForToggle = false;

        [Serializable]
        public class ControllerEntry {
            public GuidController controller;
            public VRCAvatarDescriptor.AnimLayerType type = VRCAvatarDescriptor.AnimLayerType.FX;
        }

        [Serializable]
        public class MenuEntry {
            public GuidMenu menu;
            public string prefix;
        }

        [Serializable]
        public class ParamsEntry {
            public GuidParams parameters;
        }
        
        [Serializable]
        public class BindingRewrite {
            public string from;
            public string to;
            public bool delete = false;
        }
        
        public enum SmoothingRange {
            ZeroToInfinity,
            NegOneToOne,
            Neg10kTo10k
        }

        [Serializable]
        public class SmoothParamEntry {
            public string name;
            public float smoothingDuration = 0.2f;
            public SmoothingRange range = SmoothingRange.ZeroToInfinity;
        }

#pragma warning disable 0612
        
        public override bool Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                allNonsyncedAreGlobal = true;
            }
            if (fromVersion < 2) {
                if (controller != null) {
                    controllers.Add(new ControllerEntry { controller = controller });
                    controller = null;
                }
                if (menu != null) {
                    menus.Add(new MenuEntry { menu = menu, prefix = submenu });
                    menu = null;
                }
                if (parameters != null) {
                    prms.Add(new ParamsEntry { parameters = parameters });
                    parameters = null;
                }
            }
            if (fromVersion < 3) {
                if (removePrefixes != null) {
                    foreach (var s in removePrefixes) {
                        if (!string.IsNullOrWhiteSpace(s)) {
                            rewriteBindings.Add(new BindingRewrite { from = s, to = "" });
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(addPrefix)) {
                    rewriteBindings.Add(new BindingRewrite { from = "", to = addPrefix });
                }
            }
            if (fromVersion < 4) {
                allowMissingAssets = true;
            }
            return false;
        }

        public override IList<FeatureModel> Migrate(MigrateRequest request) {
            if (useSecurityForToggle && !request.fakeUpgrade) {
                var obj = rootObjOverride;
                if (obj == null) obj = request.gameObject;
                var hasRestriction = obj.GetComponents<VRCFury>()
                    .Where(c => c != null)
                    .SelectMany(c => c.GetAllFeatures())
                    .Any(feature => feature is SecurityRestricted);
                if (!hasRestriction && !string.IsNullOrWhiteSpace(toggleParam)) {
                    var vrcf = obj.AddComponent<VRCFury>();
                    vrcf.content = new SecurityRestricted();
                    VRCFury.MarkDirty(vrcf);
                }
                useSecurityForToggle = false;
            }
            return new FeatureModel[] { this };
        }

#pragma warning restore 0612

        public override int GetLatestVersion() {
            return 4;
        }
    }
}