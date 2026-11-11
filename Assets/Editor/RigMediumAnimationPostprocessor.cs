using System;
using UnityEditor;
using UnityEngine;

public class RigMediumAnimationPostprocessor : AssetPostprocessor
{
    public const string RigMediumAnimFolder = "Assets/KayKit_Adventurers_2.0_FREE/Animations/fbx/Rig_Medium/";

    void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        if (!IsRigMediumAnimationAsset(assetPath) || clip == null)
            return;

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (EditorCurveBinding binding in bindings)
        {
            if (binding.type != typeof(Transform))
                continue;

            // Strip bone position curves so characters with different bone lengths/scales keep their proportions.
            if (binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
                continue;
            }

            // Strip root Rig_Medium scale (so Maya-scaled 46.6118 characters do not collapse to 1)
            // and strip constant 1,1,1 bone scale curves while preserving animated spawn scale curves.
            if (binding.propertyName.StartsWith("m_LocalScale.", StringComparison.Ordinal))
            {
                bool isRigRoot = string.IsNullOrEmpty(binding.path) || binding.path == "Rig_Medium";
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (isRigRoot || IsConstantScaleCurve(curve))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }
        }

        if (clip.name == "Idle_A" || clip.name == "Idle_B")
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (!settings.loopTime)
            {
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
        }
    }

    public static bool IsRigMediumAnimationAsset(string path)
    {
        return !string.IsNullOrEmpty(path)
            && path.StartsWith(RigMediumAnimFolder, StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsConstantScaleCurve(AnimationCurve curve)
    {
        if (curve == null || curve.length == 0)
            return true;

        Keyframe[] keys = curve.keys;
        float min = keys[0].value;
        float max = min;
        for (int i = 1; i < keys.Length; i++)
        {
            float value = keys[i].value;
            if (value < min) min = value;
            if (value > max) max = value;
        }
        return (max - min) < 0.001f;
    }
}

