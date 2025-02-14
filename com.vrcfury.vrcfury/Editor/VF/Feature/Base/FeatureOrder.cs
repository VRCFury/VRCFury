namespace VF.Feature.Base {
    internal enum FeatureOrder {
        
        CollectExistingComponents,
        CleanupLegacy,
        BackupBefore,

        // Needs to happen before everything
        FixDoubleFx,
        RemoveDefaultControllers,
        RemoveExtraDescriptors,

        // Needs to happen before anything starts using the Animator
        ResetAnimatorBefore,
        
        CloneAllControllers,
        
        FixAmbiguousObjectNames,
        
        // Needs to happen before toggles begin getting processed
        ApplyDuringUpload,

        // Needs to be the first thing to instantiate the ControllerManagers
        AnimatorLayerControlRecordBase,
        
        // Needs to happen before any objects are moved, so otherwise the imported
        // animations would not be adjusted to point to the new moved object paths
        FullController,
        
        UpgradeLegacyHaptics,
        GiveEverythingSpsSenders,

        // Needs to run after all haptic components are in place
        // Needs to run before Toggles, because of its "After Bake" action
        BakeHapticPlugs,
        
        ApplyImplicitRestingStates,

        Default,
        // Needs to happen after AdvancedVisemes so that gestures affecting the jaw override visemes
        SenkyGestureDriver,
        // Needs to be after anything uses GestureLeft or GestureRight
        DisableGesturesService,
        // Needs to run after all possible toggles have been created and applied
        CollectToggleExclusiveTags,
        // Needs to happen right before driving non float types
        EvaluateTriggerParams,
        // Needs to happen after all controller params (and their types) are in place
        DriveNonFloatTypes,
        
        // Needs to happen after animations are done but before objects start to move
        FixAmbiguousAnimations,

        // Needs to happen after builders have scanned their prop children objects for any purpose (since this action
        // may move objects out of the props and onto the avatar base). One example is the FullController which
        // scans the prop children for contact receivers.
        // This should be basically the only place that "moving objects" happens
        SecurityRestricted, // needs to happen before armature link so that armature linked things can inherit the security restriction
        ArmatureLink,
        WorldConstraintBuilder,

        // Needs to happen after any new skinned meshes have been added
        BoundingBoxFix,
        AnchorOverrideFix,

        // Needs to happen after toggles
        HapticsAnimationRewrites,
        
        // Needs to run after all TPS materials are done
        // Needs to run after toggles are in place
        // Needs to run after HapticsAnimationRewrites
        TpsScaleFix,
        
        ForceStateInAnimator,

        // Needs to run after everything else is done messing with rest state
        ApplyToggleRestingState,

        // Finalize Controllers
        UpgradeToVrcConstraints, // Needs to happen before any step starts looking at or cleaning up "invalid" animation bindings
        DisableSyncForAaps,
        LocalOnlyDrivenParams,
        ParameterCompressor,
        FixGestureFxConflict, // Needs to run before DirectTreeOptimizer messes with FX parameters
        BlendShapeLinkFixAnimations, // Needs to run after most things are done messing with animations, since it'll make copies of the blendshape curves
        RecordAllDefaults,
        BlendshapeOptimizer, // Needs to run after RecordDefaults
        ActionConflictResolver,
        TrackingConflictResolver,
        FixPartiallyWeightedAaps, // Needs to run before PositionDefaultsLayer, before OptimizeBlendTrees, after everything setting AAPs, after TrackingConflictResolver (creates aaps), before anything that would remove the defaults layer like CleanupEmptyLayers
        CleanupEmptyLayers, // Needs to be before anything using EnsureEmptyBaseLayer
        FixUnsetPlayableLayers,
        PositionDefaultsLayer, // Needs to be right before FixMasks so it winds up at the top of FX, right under the base mask
        FixMasks,
        LayerToTree, // Needs to run after animations are done, including everything that makes its own DBT, including TrackingConflictResolver
        AvoidMmdLayers, // Needs to be after CleanupEmptyLayers (which removes empty layers) and FixMasks and RecordAllDefaults (which may insert layers at the top)
        AnimatorLayerControlFix,
        RemoveNonQuestMaterials,
        FixTreeLength,
        TreeFlattening,
        AdjustWriteDefaults, // Needs to be after TreeFlattening, since it can change whether or not a layer has a DBT
        FixEmptyMotions, // Needs to be after AdjustWriteDefaults, since it changes behaviour if a state is WD on or off
        UpgradeWrongParamTypes,
        FinalizeController,

        // Finalize Menus
        MoveSpsMenus,
        MoveMenuItems,
        FinalizeMenu,
        FixMipmapStreaming,
        FixAudio,
        FixMenuIconTextures,

        MarkThingsAsDirtyJustInCase,
        
        // Needs to happen after everything is done using the animator, and before SaveAssets
        ResetAnimatorAfter,

        SaveAssets,
        Validation,
        HideAddedComponents,
        BackupAfter,
    }
}
