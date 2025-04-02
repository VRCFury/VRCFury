using System;
using UnityEngine;
using VF.VrcfEditorOnly;

namespace VF.Model {
    [ExecuteInEditMode]
    [AddComponentMenu("")]
    internal class VRCFuryTest : MonoBehaviour, IVrcfEditorOnly {
        public enum State {
            AddedByHarmonyPatch,
            FirstPass,
            Finished
        }
        [NonSerialized] public State state = State.Finished;

        // public static Action<VRCFuryTest> onDestroy;
        //
        // private void OnDestroy() {
        //     onDestroy?.Invoke(this);
        // }
    }
}
