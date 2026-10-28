using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 총알 프리팹(빛나는 탄두 + 짧은 빛 꼬리)을 만들고, Pools 아래에 총알 풀을 두어 플레이어 총에 연결한다.
// 꼬리 재질은 기존 BulletTracer 프리팹의 빛줄기 재질을 그대로 쓴다.
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다.
public static class BulletPrefabBuilder
{
    private const string PrefabPath = "Assets/03.Prefabs/Weapons/Bullet.prefab";
    private const string MaterialPath = "Assets/09.Materials/Weapons/BulletMat.mat";
    private const string TracerPrefabPath = "Assets/03.Prefabs/Effects/BulletTracer.prefab";
    private const string PoolsRootName = "Pools";
    private const string PoolName = "BulletPool";

    [MenuItem("Tools/Weapons/Build Bullet")]
    private static void Build()
    {
        GameObject prefab = BuildPrefab();
        if (prefab == null)
        {
            return;
        }

        Undo.SetCurrentGroupName("Build Bullet");
        int undoGroup = Undo.GetCurrentGroup();

        EffectPool pool = BuildPool(prefab);
        AssignToGun(pool);

        EditorSceneManager.MarkAllScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        Debug.Log($"총알 프리팹을 만들었습니다: {PrefabPath}");
    }

    private static GameObject BuildPrefab()
    {
        GameObject root = new GameObject("Bullet");
        try
        {
            // 탄두: 앞뒤로 길쭉한 캡슐 (기본 캡슐은 Y축으로 길어서 앞(Z)으로 눕힌다)
            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            head.transform.localScale = new Vector3(0.025f, 0.05f, 0.025f);
            head.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("New-Capsule.fbx");
            var renderer = head.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LoadOrCreateMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.06f;
            trail.minVertexDistance = 0.1f;
            trail.startWidth = 0.035f;
            trail.endWidth = 0f;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) },
            };

            var tracer = AssetDatabase.LoadAssetAtPath<GameObject>(TracerPrefabPath);
            var tracerLine = tracer != null ? tracer.GetComponent<LineRenderer>() : null;
            if (tracerLine != null)
            {
                trail.sharedMaterial = tracerLine.sharedMaterial;
            }
            else
            {
                Debug.LogWarning("BulletTracer 프리팹의 재질을 찾지 못해 총알 꼬리에 재질이 없습니다.");
            }

            // 풀이 생성 후에 붙이면 Awake 순서가 꼬이므로 프리팹에 미리 둔다.
            root.AddComponent<PooledObject>();
            var bullet = root.AddComponent<Bullet>();
            var serialized = new SerializedObject(bullet);
            serialized.FindProperty("_trail").objectReferenceValue = trail;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder("Assets/03.Prefabs/Weapons");
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Material LoadOrCreateMaterial()
    {
        EnsureFolder("Assets/09.Materials/Weapons");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        // 블룸에 걸리도록 강하게 빛나는 주황빛 탄두
        material.SetColor("_BaseColor", new Color(0.1f, 0.07f, 0.03f));
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.2f) * 6f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static EffectPool BuildPool(GameObject prefab)
    {
        GameObject poolsRoot = GameObject.Find(PoolsRootName);
        if (poolsRoot == null)
        {
            poolsRoot = new GameObject(PoolsRootName);
            Undo.RegisterCreatedObjectUndo(poolsRoot, "Build Bullet");
        }

        Transform poolTransform = poolsRoot.transform.Find(PoolName);
        if (poolTransform == null)
        {
            var poolObject = new GameObject(PoolName);
            Undo.RegisterCreatedObjectUndo(poolObject, "Build Bullet");
            poolTransform = poolObject.transform;
            poolTransform.SetParent(poolsRoot.transform, false);
        }

        EffectPool pool = poolTransform.GetComponent<EffectPool>();
        if (pool == null)
        {
            pool = Undo.AddComponent<EffectPool>(poolTransform.gameObject);
        }

        var serialized = new SerializedObject(pool);
        serialized.FindProperty("_prefab").objectReferenceValue = prefab;
        serialized.FindProperty("_defaultCapacity").intValue = 30;
        serialized.FindProperty("_maxSize").intValue = 80;
        serialized.FindProperty("_lifetime").floatValue = 0f; // 총알이 맞거나 사거리 끝에서 스스로 돌아간다.
        serialized.ApplyModifiedProperties();
        return pool;
    }

    private static void AssignToGun(EffectPool pool)
    {
        PlayerGunFire gun = Object.FindFirstObjectByType<PlayerGunFire>();
        if (gun == null)
        {
            Debug.LogWarning("씬에서 PlayerGunFire를 찾지 못해 총알 풀을 연결하지 않았습니다.");
            return;
        }

        var serialized = new SerializedObject(gun);
        serialized.FindProperty("_bulletPool").objectReferenceValue = pool;
        // 레이캐스트 무시 레이어와 미니맵 아이콘 레이어는 맞히지 않는다.
        serialized.FindProperty("_hitMask").intValue = ~(LayerMask.GetMask("Ignore Raycast", "Minimap"));
        serialized.ApplyModifiedProperties();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}
