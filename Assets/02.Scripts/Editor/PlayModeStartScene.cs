using UnityEditor;
using UnityEditor.SceneManagement;

// 에디터에서 Play를 누르면 지금 열린 씬과 상관없이 Build Settings 0번 씬(메인화면)부터 시작한다.
// 열린 씬은 그대로 두고, Play를 끝내면 원래 씬으로 돌아온다.
// 메뉴 Tools/Scene/Play From Main Menu 로 끄고 켤 수 있다. (기본: 켜짐)
[InitializeOnLoad]
public static class PlayModeStartScene
{
    private const string MenuPath = "Tools/Scene/Play From Main Menu";
    private const string PrefKey = "3DFPSgame.PlayFromMainMenu";

    static PlayModeStartScene()
    {
        // 메뉴 체크 표시는 에디터가 준비된 뒤에 갱신해야 한다.
        EditorApplication.delayCall += Apply;
    }

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }

    public static void Apply()
    {
        Menu.SetChecked(MenuPath, Enabled);

        SceneAsset firstScene = null;
        if (Enabled && EditorBuildSettings.scenes.Length > 0)
        {
            firstScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorBuildSettings.scenes[0].path);
        }

        EditorSceneManager.playModeStartScene = firstScene;
    }
}
