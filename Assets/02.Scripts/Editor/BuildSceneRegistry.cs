using System.Collections.Generic;
using System.IO;
using UnityEditor;

// Build Settings의 씬 순서를 한 곳에서 관리한다: 메인화면(0) → 시작 씬(1) → 게임 씬(2).
// 존재하는 씬만 등록한다. 씬 빌더들이 만든 직후 호출한다.
public static class BuildSceneRegistry
{
    private static readonly string[] OrderedScenes =
    {
        "Assets/01.Scenes/MainMenuScene.unity",
        "Assets/01.Scenes/StartScene.unity",
        "Assets/01.Scenes/GameScene.unity",
    };

    public static void Apply()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (string path in OrderedScenes)
        {
            if (File.Exists(path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        PlayModeStartScene.Apply();
    }
}
