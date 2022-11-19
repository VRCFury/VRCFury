namespace VF.Feature.Base {
    public enum FeatureOrder {
        // Needs to happen before toggles begin getting processed
        ForceObjectState = -1,
        
        Default = 0,
        
        // Needs to happen after AdvancedVisemes so that gestures affecting the jaw override visemes
        SenkyGestureDriver = 1,
        
        // Needs to happen after builders have scanned their prop children objects for any purpose (since this action
        // may move objects out of the props and onto the avatar base). One example is the FullController which
        // scans the prop children for contact receivers.
        ArmatureLinkBuilder = 1,
        
        // Needs to run after any builders have added their "disable blinking" models (gesture builders mostly)
        Blinking = 5,
        
        // Needs to happen after any new skinned meshes have been added
        BoundingBoxFix = 10,
        AnchorOverrideFix = 11,
        
        // Needs to run after most things are done messing with the animation controller,
        // since any changes after this won't have their animations rewritten
        ArmatureLinkBuilderFixAnimations = 99,
        
        // Needs to run after most things are done messing with animations,
        // since it'll make copies of the blendshape curves
        BlendShapeLinkFixAnimations = 100,
        
        // Needs to run after TPS integration (since it may add new TPS material meshes)
        AddOgbComponents = 101,
        
        // Needs to run after all OGB components are in place
        BakeOgbComponents = 102,
        
        // Needs to run after everything is done touching the animation controller
        FixWriteDefaults = 10000,
        
        // Needs to run after anything that creates menu items, so the user can relocate them if they wish
        MoveMenuItems = 10001,
        
        // Needs to run after all possible toggles have been created and applied
        CollectToggleExclusiveTags = 10002,
        
        // Needs to run after FixWriteDefaults collects the defaults for the defaults layer
        ApplyToggleRestingState = 10003,
    }
}
