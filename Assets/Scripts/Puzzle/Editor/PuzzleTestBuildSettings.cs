#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Unity 正在打开工程时，直接改 ProjectSettings 可能被编辑器里的旧数据盖掉。
// 脚本编译后补一次：两个新场景不在 Build Settings 里就加到末尾。
[InitializeOnLoad]
public static class PuzzleTestBuildSettings
{
    static PuzzleTestBuildSettings()
    {
        AddIfMissing("Assets/Scenes/PuzzleTest.unity");
        AddIfMissing("Assets/Scenes/FinalLevel.unity");
    }

    static void AddIfMissing(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            return;
        }

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == path)
            {
                return;
            }
        }

        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(scenes);
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("Added " + path + " to Build Settings");
    }
}
#endif
