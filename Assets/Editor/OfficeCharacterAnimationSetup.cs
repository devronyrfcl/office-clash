using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OfficeCharacterAnimationSetup
{
    const string GeneralAnimFbxPath = "Assets/KayKit_Adventurers_2.0_FREE/Animations/fbx/Rig_Medium/Rig_Medium_General.fbx";
    const string MovementAnimFbxPath = "Assets/KayKit_Adventurers_2.0_FREE/Animations/fbx/Rig_Medium/Rig_Medium_MovementBasic.fbx";
    const string ControllerPath = "Assets/Animation/AnimationControllerOfficeCH.controller";
    const string KaykitAssetsFolder = "Assets/Kaykit_assets/";
    const string CurlyDollFbxPath = "Assets/Kaykit_assets/Curly_Doll_in_Black/Curly_Doll_in_Black.fbx";

    [InitializeOnLoadMethod]
    static void AutoSetupOnLoad()
    {
        EditorApplication.delayCall += RunAutoSetupIfNeeded;
    }

    static void RunAutoSetupIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureRigMediumClipsReimported();

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
        {
            SetupController(controller);
        }

        EnsureSceneCharactersConfigured();
    }

    [MenuItem("Office Clash/Setup Office Character Animation")]
    public static void Setup()
    {
        EnsureRigMediumClipsReimported(forceAll: true);

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        SetupController(controller);
        EnsureSceneCharactersConfigured();
        AssetDatabase.SaveAssets();

        Debug.Log("Office character animations and AnimationControllerOfficeCH configured.");
    }

    static void EnsureRigMediumClipsReimported(bool forceAll = false)
    {
        ReimportIfNeeded(GeneralAnimFbxPath, "Idle_A", requireLoop: true, forceAll);
        ReimportIfNeeded(MovementAnimFbxPath, "Walking_A", requireLoop: true, forceAll);
    }

    static void ReimportIfNeeded(string fbxPath, string sampleClipName, bool requireLoop, bool force)
    {
        if (force)
        {
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
            return;
        }

        AnimationClip sampleClip = LoadClipFromFbx(fbxPath, sampleClipName);
        if (sampleClip == null)
            return;

        if (HasPositionOrScaleCurves(sampleClip) || (requireLoop && !sampleClip.isLooping))
        {
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
        }
    }

    static bool HasPositionOrScaleCurves(AnimationClip clip)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type != typeof(Transform))
                continue;

            if (binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal) ||
                binding.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    static void SetupController(AnimatorController controller)
    {
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        AnimationClip idleClip = LoadClipFromFbx(GeneralAnimFbxPath, "Idle_A");
        AnimationClip walkClip = LoadClipFromFbx(MovementAnimFbxPath, "Walking_A");
        AnimationClip throwClip = LoadClipFromFbx(GeneralAnimFbxPath, "Throw");

        if (idleClip == null || walkClip == null || throwClip == null)
        {
            Debug.LogWarning("Could not find Idle_A, Walking_A, or Throw clips in Rig_Medium FBX assets.");
            return;
        }

        for (int i = controller.layers.Length - 1; i >= 0; i--)
        {
            controller.RemoveLayer(i);
        }
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
        {
            controller.RemoveParameter(i);
        }

        controller.AddParameter("movementSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("throw", AnimatorControllerParameterType.Trigger);
        controller.AddLayer("Base Layer");

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(260f, 120f, 0f));
        idleState.motion = idleClip;
        stateMachine.defaultState = idleState;

        AnimatorState walkState = stateMachine.AddState("Walk", new Vector3(540f, 120f, 0f));
        walkState.motion = walkClip;

        AnimatorState throwState = stateMachine.AddState("Throw", new Vector3(400f, 250f, 0f));
        throwState.motion = throwClip;

        AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.hasExitTime = false;
        idleToWalk.hasFixedDuration = true;
        idleToWalk.duration = 0.15f;
        idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.01f, "movementSpeed");

        AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.hasExitTime = false;
        walkToIdle.hasFixedDuration = true;
        walkToIdle.duration = 0.15f;
        walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, "movementSpeed");

        AnimatorStateTransition anyToThrow = stateMachine.AddAnyStateTransition(throwState);
        anyToThrow.hasExitTime = false;
        anyToThrow.canTransitionToSelf = false;
        anyToThrow.hasFixedDuration = true;
        anyToThrow.duration = 0.1f;
        anyToThrow.AddCondition(AnimatorConditionMode.If, 0f, "throw");

        AnimatorStateTransition throwToWalk = throwState.AddTransition(walkState);
        throwToWalk.hasExitTime = true;
        throwToWalk.exitTime = 0.9f;
        throwToWalk.hasFixedDuration = true;
        throwToWalk.duration = 0.15f;
        throwToWalk.AddCondition(AnimatorConditionMode.Greater, 0.01f, "movementSpeed");

        AnimatorStateTransition throwToIdle = throwState.AddTransition(idleState);
        throwToIdle.hasExitTime = true;
        throwToIdle.exitTime = 0.9f;
        throwToIdle.hasFixedDuration = true;
        throwToIdle.duration = 0.15f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    static void EnsureSceneCharactersConfigured()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
            return;

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        bool sceneDirty = false;

        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                GameObject sourceAsset = PrefabUtility.GetCorrespondingObjectFromSource(animator.gameObject);
                string assetPath = sourceAsset != null ? AssetDatabase.GetAssetPath(sourceAsset) : null;

                if (string.IsNullOrEmpty(assetPath) &&
                    animator.gameObject.name.StartsWith("Curly_Doll_in_Black", StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = CurlyDollFbxPath;
                }

                if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith(KaykitAssetsFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                Avatar expectedAvatar = LoadAvatarFromFbx(assetPath);
                if (expectedAvatar != null && animator.avatar != expectedAvatar)
                {
                    Undo.RecordObject(animator, "Assign Character Avatar");
                    animator.avatar = expectedAvatar;
                    animator.applyRootMotion = false;
                    EditorUtility.SetDirty(animator);
                    sceneDirty = true;
                }

                if (string.Equals(assetPath, CurlyDollFbxPath, StringComparison.OrdinalIgnoreCase) &&
                    animator.runtimeAnimatorController == null &&
                    controller != null)
                {
                    Undo.RecordObject(animator, "Assign AnimationControllerOfficeCH");
                    animator.runtimeAnimatorController = controller;
                    EditorUtility.SetDirty(animator);
                    sceneDirty = true;
                }
            }
        }

        if (sceneDirty)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }
    }

    static AnimationClip LoadClipFromFbx(string fbxPath, string clipName)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (asset is AnimationClip clip &&
                !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                string.Equals(clip.name, clipName, StringComparison.Ordinal))
            {
                return clip;
            }
        }
        return null;
    }

    static Avatar LoadAvatarFromFbx(string fbxPath)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (asset is Avatar avatar)
                return avatar;
        }
        return null;
    }
}
