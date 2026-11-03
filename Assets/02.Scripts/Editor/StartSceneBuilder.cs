using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 시작 맵(StartScene)을 만든다: 평범한 Plane 바닥 + 플레이어 + 카메라 + 초록 포탈(E로 GameScene 이동).
// 열려 있는 씬은 건드리지 않도록 새 씬을 Additive로 만들어 저장한 뒤 닫는다.
// 다시 실행하면 같은 경로의 씬을 새로 덮어쓴다. Build Settings 등록은 BuildSceneRegistry가 맡는다.
public static class StartSceneBuilder
{
    private const string StartScenePath = "Assets/01.Scenes/StartScene.unity";
    private const string PortalPrefabPath = "Assets/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal green.prefab";

    private static readonly Vector3 PlayerPosition = new Vector3(0f, 0.1f, -6f);
    private static readonly Vector3 PortalPosition = new Vector3(0f, 0f, 6f);

    [MenuItem("Tools/Scene/Build Start Scene")]
    private static void Build()
    {
        var portalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPrefabPath);
        if (portalPrefab == null)
        {
            Debug.LogError($"포탈 프리팹을 찾지 못했습니다: {PortalPrefabPath}");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);

        CreateGround();
        CreateLight();
        Transform playerTarget = CreatePlayer(out Transform player);
        CreateCamera(playerTarget);
        CreatePortal(portalPrefab, player);

        EditorSceneManager.SaveScene(scene, StartScenePath);
        EditorSceneManager.CloseScene(scene, true);
        BuildSceneRegistry.Apply();
        Debug.Log($"시작 씬을 만들었습니다: {StartScenePath}");
    }

    private static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(5f, 1f, 5f);

        // 렌더 파이프라인 기본 머티리얼을 써서 분홍색으로 보이는 것을 막는다.
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline != null && pipeline.defaultMaterial != null)
        {
            ground.GetComponent<MeshRenderer>().sharedMaterial = pipeline.defaultMaterial;
        }
    }

    private static void CreateLight()
    {
        var go = new GameObject("Directional Light");
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.shadows = LightShadows.Soft;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    // 플레이어 오브젝트를 만들고, 카메라가 따라갈 눈높이 대상 Transform을 돌려준다.
    private static Transform CreatePlayer(out Transform player)
    {
        var go = new GameObject("Player");
        go.transform.position = PlayerPosition;

        var controller = go.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        go.AddComponent<PlayerRotate>();
        go.AddComponent<PlayerMove>();

        var eye = new GameObject("CameraTarget");
        eye.transform.SetParent(go.transform, false);
        eye.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        player = go.transform;
        return eye.transform;
    }

    private static void CreateCamera(Transform target)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        go.transform.position = target.position;
        go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        go.GetComponent<Camera>().GetUniversalAdditionalCameraData();

        var rotate = go.AddComponent<CameraRotate>();
        var follow = go.AddComponent<CameraFollow>();
        go.AddComponent<CameraViewSwitcher>();

        var so = new SerializedObject(follow);
        so.FindProperty("_target").objectReferenceValue = target;
        so.FindProperty("_cameraRotate").objectReferenceValue = rotate;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreatePortal(GameObject prefab, Transform player)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = PortalPosition;
        // 플레이어(−Z 쪽)를 향하도록 돌린다.
        instance.transform.rotation = Quaternion.LookRotation(Vector3.back);

        var portal = instance.AddComponent<Portal>();
        var so = new SerializedObject(portal);
        so.FindProperty("_player").objectReferenceValue = player;
        so.FindProperty("_targetSceneName").stringValue = "GameScene";
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
