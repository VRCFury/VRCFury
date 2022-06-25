using System;
using UnityEngine;

[Serializable]
public class SenkyFXAction {
    public const string TOGGLE = "toggle";
    public const string BLENDSHAPE = "blendShape";

    public string type;
    public GameObject obj;
    public string blendShape;
}
