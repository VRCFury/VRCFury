using System;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal class ShowInFirstPerson : NewFeatureModel {
        [NonSerialized] public bool useObjOverride = false;
        [NonSerialized] public GameObject objOverride = null;
        [NonSerialized] public bool onlyIfChildOfHead = false;
    }
}