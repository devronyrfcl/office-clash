#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class OfficeCredentialsIosBuild
{
    [PostProcessBuild]
    public static void AddSecurityFramework(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string projectPath = PBXProject.GetPBXProjectPath(path);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "Security.framework", false);
        project.WriteToFile(projectPath);
    }
}
#endif
