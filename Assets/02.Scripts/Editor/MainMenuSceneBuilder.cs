using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

// 메인화면 씬(MainMenuScene)을 만든다: 카메라에 직접 그리는 전체 화면 영상 + 영상 속 LOG IN / QUIT 버튼 위에 겹친 투명 버튼.
// 다른 씬을 건드리지 않도록 새 씬을 Additive로 만들어 저장한 뒤 닫는다. 다시 실행하면 씬을 새로 덮어쓴다.
// (메인화면 씬 자체가 열려 있으면 잠시 닫았다가 만든 뒤 다시 연다)
public static class MainMenuSceneBuilder
{
    private const string ScenePath = "Assets/01.Scenes/MainMenuScene.unity";
    private const string VideoPath = "Assets/04.Images/glitch_transition_v4.mp4";

    // 영상(1920x1080)의 마지막 프레임에서 버튼이 그려진 영역(픽셀, 좌상단 기준).
    private const float VideoWidth = 1920f;
    private const float VideoHeight = 1080f;
    private static readonly Rect LoginRect = new Rect(1070f, 658f, 370f, 82f);
    private static readonly Rect QuitRect = new Rect(1468f, 658f, 289f, 82f);

    [MenuItem("Tools/Scene/Build Main Menu Scene")]
    private static void Build()
    {
        var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
        if (clip == null)
        {
            Debug.LogError($"영상 클립을 찾지 못했습니다(VideoClip으로 임포트되어야 함): {VideoPath}");
            return;
        }

        // 메인화면 씬 자체가 열려 있거나(같은 경로로 저장 불가) 저장 안 된 빈 씬만 열려 있으면(Additive 생성 불가)
        // 새 씬을 Single로 만들어 열린 씬을 대신하고, 저장 후 메인화면 씬을 다시 연다.
        bool replaceOpenScenes = SceneManager.GetSceneByPath(ScenePath).isLoaded || OnlyUntitledSceneOpen();
        if (replaceOpenScenes && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            replaceOpenScenes ? NewSceneMode.Single : NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        Camera camera = CreateCamera();
        VideoPlayer video = CreateVideoPlayer(clip, camera);
        RectTransform stage = CreateCanvasAndStage();
        CanvasGroup group = CreateButtonGroup(stage, out Button login, out Button quit);
        CreateEventSystem();
        CreateMainMenu(video, group, login, quit);

        EditorSceneManager.SaveScene(scene, ScenePath);
        if (replaceOpenScenes)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        BuildSceneRegistry.Apply();
        Debug.Log($"메인화면 씬을 만들었습니다: {ScenePath}");
    }

    private static bool OnlyUntitledSceneOpen()
    {
        return SceneManager.sceneCount == 1 && string.IsNullOrEmpty(SceneManager.GetSceneAt(0).path);
    }

    private static Camera CreateCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        go.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData();
        return cam;
    }

    // 렌더 텍스처를 거치지 않고 카메라 배경에 바로 그린다.
    // (렌더 텍스처 에셋은 이전 실행의 마지막 프레임을 기억해서, 시작할 때 로그인 화면이 먼저 비치는 문제가 있었다)
    private static VideoPlayer CreateVideoPlayer(VideoClip clip, Camera camera)
    {
        var go = new GameObject("VideoPlayer");
        var video = go.AddComponent<VideoPlayer>();
        video.source = VideoSource.VideoClip;
        video.clip = clip;
        video.renderMode = VideoRenderMode.CameraFarPlane;
        video.targetCamera = camera;
        video.aspectRatio = VideoAspectRatio.FitInside;
        video.playOnAwake = true;
        video.isLooping = false;
        video.waitForFirstFrame = true;
        video.skipOnDrop = true;
        video.audioOutputMode = VideoAudioOutputMode.Direct;
        return video;
    }

    // 영상 비율(16:9)을 유지한 채 화면에 맞추는 Stage. 버튼은 Stage의 자식이라 영상과 항상 같은 위치에 겹친다.
    private static RectTransform CreateCanvasAndStage()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(VideoWidth, VideoHeight);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var stageGo = new GameObject("Stage", typeof(RectTransform));
        stageGo.transform.SetParent(canvasGo.transform, false);
        var stage = (RectTransform)stageGo.transform;
        Stretch(stage);
        var fitter = stageGo.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = VideoWidth / VideoHeight;
        return stage;
    }

    private static CanvasGroup CreateButtonGroup(RectTransform stage, out Button login, out Button quit)
    {
        var go = new GameObject("Buttons", typeof(RectTransform));
        go.transform.SetParent(stage, false);
        Stretch((RectTransform)go.transform);
        var group = go.AddComponent<CanvasGroup>();

        login = CreateButton(go.transform, "LoginButton", LoginRect);
        quit = CreateButton(go.transform, "QuitButton", QuitRect);
        return group;
    }

    // 평소엔 완전 투명(영상이 그대로 보임), 마우스를 올리면 HoverHighlight가 초록 하이라이트를 켠다.
    // Image 기본 색 자체를 투명으로 두어, 에디터 화면이나 첫 프레임에도 박스가 보이지 않게 한다.
    private static Button CreateButton(Transform parent, string name, Rect pixelRect)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(pixelRect.xMin / VideoWidth, 1f - pixelRect.yMax / VideoHeight);
        rt.anchorMax = new Vector2(pixelRect.xMax / VideoWidth, 1f - pixelRect.yMin / VideoHeight);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        go.AddComponent<HoverHighlight>();
        return button;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void CreateMainMenu(VideoPlayer video, CanvasGroup group, Button login, Button quit)
    {
        var go = new GameObject("MainMenu");
        var menu = go.AddComponent<MainMenu>();
        var so = new SerializedObject(menu);
        so.FindProperty("_video").objectReferenceValue = video;
        so.FindProperty("_buttonGroup").objectReferenceValue = group;
        so.FindProperty("_loginButton").objectReferenceValue = login;
        so.FindProperty("_quitButton").objectReferenceValue = quit;
        so.FindProperty("_startSceneName").stringValue = "StartScene";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
