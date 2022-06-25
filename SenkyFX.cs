using System;
using UnityEngine;

public class SenkyFX : MonoBehaviour {
    public SenkyFXState stateBlink;
    public string visemeFolder;

    public GameObject breatheObject;
    public string breatheBlendshape;
    public float breatheScaleMin;
    public float breatheScaleMax;

    public SenkyFXState stateToesDown;
    public SenkyFXState stateToesUp;
    public SenkyFXState stateToesSplay;

    public SenkyFXState stateEyesClosed;
    public SenkyFXState stateEyesHappy;
    public SenkyFXState stateEyesSad;
    public SenkyFXState stateEyesAngry;

    public SenkyFXState stateMouthBlep;
    public SenkyFXState stateMouthSuck;
    public SenkyFXState stateMouthSad;
    public SenkyFXState stateMouthAngry;
    public SenkyFXState stateMouthHappy;

    public SenkyFXState stateEarsBack;

    public SenkyFXState stateTalkGlow;

    public SenkyFXProps props;
}
