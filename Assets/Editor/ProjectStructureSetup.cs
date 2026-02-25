// Assets/Editor/ProjectStructureSetup.cs
// 메뉴: UIShader > Setup Project Structure
// Unity 프로젝트 내 UIShader 전용 폴더 구조를 자동으로 생성한다.

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using UnityEngine;

public static class ProjectStructureSetup
{
    private static readonly string[] Folders = new string[]
    {
        "Assets/Scenes",
        "Assets/Scripts/Core",
        "Assets/Scripts/CEF",
        "Assets/Scripts/DepthMap",
        "Assets/Scripts/Rendering",
        "Assets/Scripts/Camera",
        "Assets/Scripts/UI",
        "Assets/Shaders/PostProcessing",
        "Assets/Materials",
        "Assets/Models",
        "Assets/Textures/Test",
        "Assets/Textures/Studio",
        "Assets/ScriptableObjects/DepthWeightPresets",
        "Assets/Plugins/CEF/Windows/x86_64",
        "Assets/Plugins/CEF/macOS/arm64",
        "Assets/JavaScript",
        "Assets/Editor"
    };

    [MenuItem("UIShader/Setup Project Structure", false, 0)]
    public static void Setup()
    {
        int created = 0;
        int existing = 0;

        foreach (string folder in Folders)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                created++;
            }
            else
            {
                existing++;
            }
        }

        AssetDatabase.Refresh();

        Debug.Log($"[UIShader] 프로젝트 구조 설정 완료: " +
                  $"{created}개 폴더 생성, {existing}개 이미 존재");
    }

    [MenuItem("UIShader/Setup Project Structure", true)]
    private static bool SetupValidate()
    {
        // Assets 폴더가 있을 때만 활성화
        return Directory.Exists("Assets");
    }
}
#endif
