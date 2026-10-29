using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// "Fast Mobile Black Hole URP"의 블랙홀로 폭발하는 장애물 프리팹을 만들고, 맵에 일정 간격으로 만드는 스포너를 씬에 둔다. (옛 드럼통 대신). 폭발 이펙트는 ParticleProFX의 ppfxExplosionHeavy.
// 크기: 전체 지름 12m(드럼통 높이 2m의 6배). 원본 프리팹의 지름은 10m라 1.2배로 키운다. 멀리서도 보이도록 크게 했다.
// 맞는 곳은 눈에 보이는 소용돌이 크기의 구 하나다. (원본은 납작한 원판이라 총알이 거의 안 맞는다)
// 이름으로 찾아 덮어쓰므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다.
public static class BlackHoleBuilder
{
    private const string SourcePath = "Assets/Fast Mobile Black Hole URP/Prefabs/BlackHole.prefab";
    private const string OutputPath = "Assets/03.Prefabs/Environment/BlackHoleBomb.prefab";
    private const string ExplosionPath = "Assets/ParticleProFX/Resources/Library/Fire & Explosions/ppfxExplosionHeavy.prefab";
    private const string SpawnerName = "BlackHoleSpawner";
    private const float VisualScale = 1.2f;     // 원본 지름 10m → 12m
    private const float HitRadius = 4.8f;
    private const float ExplosionRange = 28f;   // 커진 만큼 폭발 범위·이펙트도 키운다.
    private const float EffectScale = 6f;
    private const float MinPlayerDistance = 15f; // 커진 만큼 플레이어 근처에는 만들지 않는다.
    private const float HoverHeight = 5f;       // 땅에서 이만큼 위에 띄운다. (반지름 6m라 일부는 땅에 묻힌다)
    private const string DrumLayer = "Drum";

    [MenuItem("Tools/Map/Build Black Holes")]
    private static void Build()
    {
        // 플레이 중에 만들면 새 컴포넌트의 Awake가 돌아 참조 오류가 나고 씬을 더럽힐 수도 없다.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("플레이 모드에서는 블랙홀을 만들 수 없습니다. 플레이를 멈추고 다시 실행하세요.");
            return;
        }

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        GameObject player = GameObject.Find("Player");
        var explosion = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);
        if (source == null || player == null || explosion == null)
        {
            Debug.LogError("블랙홀 프리팹, Player 또는 폭발 이펙트(ParticleProFX)를 찾지 못했습니다.");
            return;
        }

        BlackHole prefab = BuildPrefab(source, explosion);
        BuildSpawner(prefab, player.transform);

        AssetDatabase.SaveAssets();
        Debug.Log("블랙홀 프리팹과 스포너를 만들었습니다. 결과를 확인한 뒤 씬을 저장하세요.");
    }

    private static BlackHole BuildPrefab(GameObject source, GameObject explosion)
    {
        var root = new GameObject("BlackHoleBomb");
        try
        {
            root.layer = LayerMask.NameToLayer(DrumLayer);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * VisualScale;

            // 원본의 콜라이더(납작한 원판 등)는 지우고, 루트의 구 하나로 대신한다.
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = HitRadius;

            var blackHole = root.AddComponent<BlackHole>();
            var serialized = new SerializedObject(blackHole);
            serialized.FindProperty("_explosionEffectPrefab").objectReferenceValue = explosion;
            serialized.FindProperty("_visual").objectReferenceValue = visual.transform;
            serialized.FindProperty("_explosionRange").floatValue = ExplosionRange;
            serialized.FindProperty("_effectScale").floatValue = EffectScale;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            blackHole.enabled = true; // 참조를 연결하기 전에 Awake가 돌았다면 꺼져 있을 수 있다.

            EnsureFolder("Assets/03.Prefabs/Environment");
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
            return saved.GetComponent<BlackHole>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void BuildSpawner(BlackHole prefab, Transform player)
    {
        GameObject spawnerObject = GameObject.Find(SpawnerName);
        if (spawnerObject == null)
        {
            spawnerObject = new GameObject(SpawnerName);
            Undo.RegisterCreatedObjectUndo(spawnerObject, "Build Black Holes");
        }

        var spawner = spawnerObject.GetComponent<BlackHoleSpawner>();
        if (spawner == null)
        {
            spawner = Undo.AddComponent<BlackHoleSpawner>(spawnerObject);
        }

        var serialized = new SerializedObject(spawner);
        serialized.FindProperty("_prefab").objectReferenceValue = prefab;
        serialized.FindProperty("_player").objectReferenceValue = player;
        serialized.FindProperty("_hoverHeight").floatValue = HoverHeight;
        serialized.FindProperty("_minPlayerDistance").floatValue = MinPlayerDistance;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(spawnerObject.scene);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
