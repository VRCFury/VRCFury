namespace VF.Feature.Base {
    public enum FeatureOrder {
        CleanupLegacy = -2000,
        
        // Needs to happen before ForceObjectState
        FullControllerToggle = -1600,

        // Needs to happen before everything
        FixDoubleFx = -1000,

        // Needs to happen before anything starts using the Animator
        ResetAnimatorBefore = -101,
        
        // Needs to happen before toggles begin getting processed
        ForceObjectState = -51,
        ApplyRestState1 = -50,
        
        // Needs to be the first thing to instantiate the ControllerManagers
        AnimatorLayerControlRecordBase = -10,
        
        // Needs to happen before any objects are moved, so otherwise the imported
        // animations would not be adjusted to point to the new moved object paths
        FullController = -5,
        
        UpgradeLegacyHaptics = -4,
        
        // Needs to run after all haptic components are in place
        // Needs to run before Toggles, because of its "After Bake" action
        BakeHaptics = -3,

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
        
        // Needs to run after all TPS materials are done
        // Needs to run after toggles are in place
        TpsScaleFix = 120,

        // Needs to run before ObjectMoveBuilderFixAnimations, but after anything that needs
        // an object moved onto the fake head bone
        FakeHeadBuilder = 130,

        HapticsAnimationRewrites = 145,
        
        // Needs to run after everything else is done messing with rest state
        ApplyRestState2 = 146,
        ApplyToggleRestingState = 147,
        ApplyRestState3 = 148,
        
        // Needs to run after most things are done messing with animations,
        // since it'll make copies of the blendshape curves
        BlendShapeLinkFixAnimations = 150,
        
        // Needs to run after animations are done, but before FixWriteDefaults
        DirectTreeOptimizer = 8000,

        // Needs to run after everything is done touching the animation controller
        FixWriteDefaults = 10000,
        
        // Needs to run after anything that creates menu items, so the user can relocate them if they wish
        SetMenuIcons1 = 10001,
        MoveMenuItems = 10002,
        SetMenuIcons2 = 10003,
        
        // Needs to run after all animations are locked in and done
        BlendshapeOptimizer = 10011,
        Slot4Fix = 10012,
        
        // Needs to happen after everything is done adding / removing controller layers
        CleanupEmptyLayers = 10019,
        PullMusclesOutOfFx = 10020,
        RestoreProxyClips = 10021, // needs to be after PullMusclesOutOfFx, which uses proxy clips
        FixMasks = 10022,
        FixMaterialSwapWithMask = 10023,
        ControllerConflictCheck = 10024,
        AnimatorLayerControlFix = 10025,

        FixBadParameters = 10026,
        
        FinalizeParams = 10030,
        FinalizeMenu = 10031,
        FinalizeController = 10032,
        MarkThingsAsDirtyJustInCase = 10033,
        
        RemoveJunkAnimators = 11000,
        
        // Needs to happen after everything is done using the animator
        ResetAnimatorAfter = 12000,
    }
}
