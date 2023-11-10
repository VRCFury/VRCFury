namespace VF.Feature.Base {
    public enum FeatureOrder {

        CleanupLegacy,
        
        // Needs to happen before everything
        FixDoubleFx,
        
        // Needs to happen before ForceObjectState
        FullControllerToggle,

        // Needs to happen before anything starts using the Animator
        ResetAnimatorBefore,
        
        // Needs to happen before toggles begin getting processed
        DeleteDuringUpload,
        ApplyDuringUpload,
        RemoveEditorOnly,

        // Needs to be the first thing to instantiate the ControllerManagers
        AnimatorLayerControlRecordBase,
        
        // Needs to happen before any objects are moved, so otherwise the imported
        // animations would not be adjusted to point to the new moved object paths
        FullController,
        
        UpgradeLegacyHaptics,
        
        // Needs to run after all haptic components are in place
        // Needs to run before Toggles, because of its "After Bake" action
        ApplyRestState1,
        BakeHapticPlugs,
        ApplyRestState2,

        Default,
        
        // Needs to happen after AdvancedVisemes so that gestures affecting the jaw override visemes
        SenkyGestureDriver,
        
        // Needs to happen after builders have scanned their prop children objects for any purpose (since this action
        // may move objects out of the props and onto the avatar base). One example is the FullController which
        // scans the prop children for contact receivers.
        ArmatureLinkBuilder,
        
        // Needs to run after all possible toggles have been created and applied
        CollectToggleExclusiveTags,
        
        // Needs to run after any builders have added their "disable blinking" models (gesture builders mostly)
        Blinking,
        
        // Needs to happen after any new skinned meshes have been added
        BoundingBoxFix,
        AnchorOverrideFix,

        // Needs to run before ObjectMoveBuilderFixAnimations, but after anything that needs
        // an object moved onto the fake head bone
        FakeHeadBuilder,

        // Needs to happen after toggles
        HapticsAnimationRewrites,
        
        // Needs to run after all TPS materials are done
        // Needs to run after toggles are in place
        // Needs to run after HapticsAnimationRewrites
        TpsScaleFix,
        DpsTipScaleFix,
        
        FixTouchingContacts,

        // Needs to run after everything else is done messing with rest state
        ApplyRestState3,
        ApplyToggleRestingState,
        ApplyRestState4,

        // Finalize Controllers
        BlendShapeLinkFixAnimations, // Needs to run after most things are done messing with animations, since it'll make copies of the blendshape curves
        DirectTreeOptimizer, // Needs to run after animations are done, but before RecordDefaults
        RecordAllDefaults,
        BlendshapeOptimizer, // Needs to run after RecordDefaults
        Slot4Fix,
        CleanupEmptyLayers,
        PullMusclesOutOfFx,
        RemoveDefaultedAdditiveLayer,
        FixUnsetPlayableLayers,
        FixMasks,
        FixMaterialSwapWithMask,
        ControllerConflictCheck,
        AdjustWriteDefaults,
        FixEmptyMotions,
        AnimatorLayerControlFix,
        RemoveNonQuestMaterials,
        RemoveBadControllerTransitions,
        FinalizeController,

        // Finalize Menus
        MoveSpsMenus,
        MoveMenuItems,
        FinalizeMenu,

        // Finalize Parameters
        FixBadParameters,
        FinalizeParams,

        MarkThingsAsDirtyJustInCase,
        
        RemoveJunkAnimators,

        // Needs to be at the very end, because it places immutable clips into the avatar
        RestoreProxyClips,
        // Needs to happen after everything is done using the animator
        ResetAnimatorAfter,

        SaveAssets,
    }
}
