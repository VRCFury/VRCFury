using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class AssetDatabaseExamples : MonoBehaviour {
  [MenuItem("Assets/Generate Controller From Override", true)]
  private static bool ValidateMenuOption()
  {
      return Selection.activeObject != null
          && Selection.activeObject.GetType() == typeof(AnimatorOverrideController);
  }

  [MenuItem("Assets/Generate Controller From Override")]
  static void Generate() {
    var o = Selection.activeObject;

    var overrideController = (AnimatorOverrideController)o;
    var overrideAssetPath = AssetDatabase.GetAssetPath(overrideController);
    var baseRuntime = overrideController.runtimeAnimatorController;
    var baseAssetPath = AssetDatabase.GetAssetPath(baseRuntime);

    var overridesList = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
    overrideController.GetOverrides(overridesList);
    Dictionary<AnimationClip, AnimationClip> overrides = new Dictionary<AnimationClip, AnimationClip>();
    foreach (var entry in overridesList) {
      overrides.Add(entry.Key, entry.Value);
      //Debug.LogWarning(entry.Key + " -> " + entry.Value);
    }

    var basePath = Regex.Replace(overrideAssetPath, @"\.[\.a-zA-Z]*$", "");
    var copyPath = basePath + ".controller";
    var tmpPath = basePath + ".tmp.controller";
    AssetDatabase.CopyAsset(baseAssetPath, tmpPath);
    var copyRuntime = (RuntimeAnimatorController)AssetDatabase.LoadMainAssetAtPath(tmpPath);

    var copy = (AnimatorController)copyRuntime;
    foreach (var layer in copy.layers) {
      ApplyOverrides(layer.stateMachine, overrides);
    }

    // Do tricks to make it import the 'new' controller using the old guid
    AssetDatabase.DeleteAsset(copyPath);
    AssetDatabase.SaveAssets();
    FileUtil.ReplaceFile(tmpPath, copyPath);
    FileUtil.DeleteFileOrDirectory(tmpPath);
    FileUtil.DeleteFileOrDirectory(tmpPath + ".meta");
    AssetDatabase.Refresh();
  }

  static void ApplyOverrides(
    AnimatorStateMachine stateMachine,
    Dictionary<AnimationClip, AnimationClip> overrides
  ) {
    foreach (var child in stateMachine.stateMachines) {
      ApplyOverrides(child.stateMachine, overrides);
    }
    foreach (var child in stateMachine.states) {
      var state = child.state;
      state.motion = ApplyOverrides(state.motion, overrides);
    }
  }

  static Motion ApplyOverrides(
    Motion motion,
    Dictionary<AnimationClip, AnimationClip> overrides
  ) {
    if (motion == null) {
      return null;
    }
    if (motion.GetType() == typeof(AnimationClip)) {
      var anim = (AnimationClip)motion;
      if (overrides.ContainsKey(anim) && overrides[anim] != null) {
        return overrides[anim];
      }
    }
    if (motion.GetType() == typeof(BlendTree)) {
      var blendTree = (BlendTree)motion;
      //Debug.LogWarning("Checking BlendTree " + blendTree);
      var blendStates = blendTree.children;
      while (blendTree.children.Length > 0) {
        blendTree.RemoveChild(0);
      }
      foreach (var blendState in blendStates) {
        var newMotion = ApplyOverrides(blendState.motion, overrides);
        //Debug.LogWarning(blendState.motion + " -> " + newMotion);
        if (blendTree.blendType == BlendTreeType.Simple1D) {
          blendTree.AddChild(newMotion, blendState.threshold);
        } else {
          blendTree.AddChild(newMotion, blendState.position);
        }
      }
    }

    return motion;
  }
}
