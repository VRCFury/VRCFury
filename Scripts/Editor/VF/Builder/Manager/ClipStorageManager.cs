using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Builder {
    public class ClipStorageManager {
        private readonly static string prefix = "VRCFury";
        private readonly string tmpDir;
        private readonly List<Object> created = new List<Object>();
        private readonly List<string> usedNames = new List<string>();
        private readonly Func<int> currentFeatureNumProvider;

        public ClipStorageManager(string tmpDir, Func<int> currentFeatureNumProvider) {
            this.tmpDir = tmpDir;
            this.currentFeatureNumProvider = currentFeatureNumProvider;
        }

        private AnimationClip _noopClip;
        public AnimationClip GetNoopClip() {
            if (_noopClip == null) {
                _noopClip = _NewClip("noop");
                _noopClip.SetCurve("_ignored", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0,0,0));
            }
            return _noopClip;
        }
        
        private Object _clipStorageObj;
        private void AddToClipStorage(Object asset) {
            var baseName = asset.name;
            for (int i = 0;; i++) {
                asset.name = baseName + (i > 0 ? " " + i : "");
                if (!usedNames.Contains(asset.name)) break;
            }
            usedNames.Add(asset.name);
            created.Add(asset);

            if (_clipStorageObj == null) {
                _clipStorageObj = new AnimationClip();
                _clipStorageObj.hideFlags = HideFlags.None;
                VRCFuryAssetDatabase.SaveAsset(_clipStorageObj, tmpDir, "VRCF_Clips");
            }
            AssetDatabase.AddObjectToAsset(asset, _clipStorageObj);
        }

        public AnimationClip NewClip(string name) {
            return _NewClip(currentFeatureNumProvider.Invoke() + "_" + name);
        }
        private AnimationClip _NewClip(string name) {
            var clip = new AnimationClip();
            clip.name = prefix + "/" + name;
            clip.hideFlags = HideFlags.None;
            AddToClipStorage(clip);
            return clip;
        }
        public BlendTree NewBlendTree(string name) {
            return _NewBlendTree(currentFeatureNumProvider.Invoke() + "_" + name);
        }
        private BlendTree _NewBlendTree(string name) {
            var tree = new BlendTree();
            tree.name = prefix + "/" + name;
            tree.hideFlags = HideFlags.None;
            AddToClipStorage(tree);
            return tree;
        }

        public void Finish() {
            foreach (var c in created) {
                EditorUtility.SetDirty(c);
            }
        }
    }
}