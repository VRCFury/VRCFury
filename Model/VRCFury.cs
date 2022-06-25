using System;
using UnityEngine;

namespace VRCF.Model {

public class VRCFury : MonoBehaviour {
    public VRCFuryState stateBlink;
    public string visemeFolder;

    public GameObject breatheObject;
    public string breatheBlendshape;
    public float breatheScaleMin;
    public float breatheScaleMax;

    public VRCFuryState stateToesDown;
    public VRCFuryState stateToesUp;
    public VRCFuryState stateToesSplay;

    public VRCFuryState stateEyesClosed;
    public VRCFuryState stateEyesHappy;
    public VRCFuryState stateEyesSad;
    public VRCFuryState stateEyesAngry;

    public VRCFuryState stateMouthBlep;
    public VRCFuryState stateMouthSuck;
    public VRCFuryState stateMouthSad;
    public VRCFuryState stateMouthAngry;
    public VRCFuryState stateMouthHappy;

    public VRCFuryState stateEarsBack;

    public VRCFuryState stateTalkGlow;

    public VRCFuryProps props;
}

}
