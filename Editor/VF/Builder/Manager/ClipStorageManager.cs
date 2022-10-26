using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Builder {
    public class ClipStorageManager {
        private static string prefix = "VRCFury";
        private string tmpDir;
        private List<Object> created = new List<Object>();
        private List<string> usedNames = new List<string>();

        public ClipStorageManager(string tmpDir) {
            this.tmpDir = tmpDir;
        }

        private AnimationClip _noopClip;
        public AnimationClip GetNoopClip() {
            if (_noopClip == null) {
                _noopClip = NewClip("noop");
                _noopClip.SetCurve("_ignored", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0,0,0));
            }
            return _noopClip;
        }
        
        private Object _clipStorageObj;
        public void AddToClipStorage(Object asset) {
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
            var clip = new AnimationClip();
            clip.name = prefix + "/" + name;
            clip.hideFlags = HideFlags.None;
            AddToClipStorage(clip);
            return clip;
        }
        public BlendTree NewBlendTree(string name) {
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