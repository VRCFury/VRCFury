using System;
using UnityEngine;

namespace VRCF.Model {

[Serializable]
public class VRCFuryAction {
    public const string TOGGLE = "toggle";
    public const string BLENDSHAPE = "blendShape";

    public string type;
    public GameObject obj;
    public string blendShape;
}

}
