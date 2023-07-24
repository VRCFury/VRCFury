namespace VF.Feature.Base {
    public enum FeatureOrder {
        CleanupLegacy = -2000,
        
        // Needs to happen before toggles begin getting processed
        ForceObjectState = -1500,
        
        // Needs to happen before everything
        FixDoubleFx = -1000,

        // Needs to happen before anything starts using the Animator
        ResetAnimatorBefore = -101,
        FixDuplicateArmature = -100,
        
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
        
        // Needs to run after most things are done messing with the animation controller,
        // since any changes after this won't have their animations rewritten
        // Needs to run after things are done moving objects
        ObjectMoveBuilderFixAnimations = 140,
        
        HapticsAnimationRewrites = 145,
        
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
        CleanupBaseMasks = 10020,
        CleanupEmptyLayers = 10021,
        ControllerConflictCheck = 10022,
        AnimatorLayerControlFix = 10023,

        FixBadParameters = 10024,
        
        FinalizeParams = 10030,
        FinalizeMenu = 10031,
        FinalizeController = 10032,
        MarkThingsAsDirtyJustInCase = 10033,
        
        RemoveJunkAnimators = 11000,
        
        // Needs to happen after everything is done using the animator
        ResetAnimatorAfter = 12000,
    }
}
