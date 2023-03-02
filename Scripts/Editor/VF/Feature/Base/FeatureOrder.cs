namespace VF.Feature.Base {
    public enum FeatureOrder {
        CleanupLegacy = -2000,
        
        // Needs to happen before everything
        FixDoubleFx = -1000,

        // Needs to happen before anything starts using the Animator
        ResetAnimatorBefore = -101,
        FixDuplicateArmature = -100,
        
        // Needs to be the first thing to instantiate the ControllerManagers
        AnimatorLayerControlRecordBase = -10,
        
        // Needs to happen before toggles begin getting processed
        ForceObjectState = -1,
        
        Default = 0,
        
        // Needs to happen after AdvancedVisemes so that gestures affecting the jaw override visemes
        SenkyGestureDriver = 1,
        
        // Needs to happen after builders have scanned their prop children objects for any purpose (since this action
        // may move objects out of the props and onto the avatar base). One example is the FullController which
        // scans the prop children for contact receivers.
        ArmatureLinkBuilder = 1,
        
        // Needs to run after all possible toggles have been created and applied
        CollectToggleExclusiveTags = 1,
        
        // Needs to run after any builders have added their "disable blinking" models (gesture builders mostly)
        Blinking = 5,
        
        // Needs to happen after any new skinned meshes have been added
        BoundingBoxFix = 10,
        AnchorOverrideFix = 11,

        // Needs to run after TPS integration (since it may add new TPS material meshes)
        AddOgbComponents = 100,
        
        // Needs to run after all OGB components are in place
        BakeOgbComponents = 101,
        
        // Needs to run before ObjectMoveBuilderFixAnimations, but after anything that needs
        // an object moved onto the fake head bone
        FakeHeadBuilder = 102,
        
        // Needs to run after most things are done messing with the animation controller,
        // since any changes after this won't have their animations rewritten
        ObjectMoveBuilderFixAnimations = 103,
        
        // Needs to run after most things are done messing with animations,
        // since it'll make copies of the blendshape curves
        BlendShapeLinkFixAnimations = 104,
        
        // Needs to run after animations are done, but before FixWriteDefaults
        DirectTreeOptimizer = 8000,

        // Needs to run after everything is done touching the animation controller
        FixWriteDefaults = 10000,
        
        // Needs to run after anything that creates menu items, so the user can relocate them if they wish
        SetMenuIcons1 = 10001,
        MoveMenuItems = 10002,
        SetMenuIcons2 = 10003,

        // Needs to run after FixWriteDefaults collects the defaults for the defaults layer
        ApplyToggleRestingState = 10005,
        
        // Needs to run after all animations are locked in and done
        BlendshapeOptimizer = 10011,
        Slot4Fix = 10012,
        
        // Needs to happen after everything is done adding / removing controller layers
        CleanupBaseMasks = 10020,
        CleanupEmptyLayers = 10021,
        AnimatorLayerControlFix = 10022,
        ControllerConflictCheck = 10023,
        
        RemoveJunkAnimators = 11000,
        
        // Needs to happen after everything is done using the animator
        ResetAnimatorAfter = 12000,

        // This messes with the raw controller on the avatar, so it has to run after we've done basically everything
        D4rkOptimizer = 99999,
    }
}
