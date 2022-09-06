namespace VF.Feature.Base {
    public enum FeatureOrder {
        // Needs to happen after BlinkController
        // Needs to happen after AdvancedVisemes so that gestures affecting the jaw override visemes
        SenkyGestureDriver = 1,
        
        // Needs to happen after any new skinned meshes have been added
        BoundingBoxFix = 10,
        AnchorOverrideFix = 11,
        
        // Needs to run after most things are done messing with the animation controller,
        // since any changes after this won't have their animations rewritten
        ArmatureLinkBuilderFixAnimations = 100,
        
        // Needs to run after TPS integration (since it may add new TPS material meshes)
        AddOgbComponents = 101,
        
        // Needs to run after everything is done touching the animation controller
        FixWriteDefaults = 10000,
        
        // Needs to run after anything that creates menu items, so the user can relocate them if they wish
        MoveMenuItems = 10001
    }
}
