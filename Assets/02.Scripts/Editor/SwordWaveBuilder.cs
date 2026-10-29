using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Hovl Studio의 검기(Slash) 이펙트를 모두 초록색 복사본으로 만들고, 각각을 풀에 담아 플레이어 총에 연결한다.
// 원본 프리팹과 재질은 건드리지 않는다. 복사본 프리팹과 초록 재질은 Assets/03.Prefabs/Weapons/SwordWaves, Assets/09.Materials/Weapons/SwordWaves에 만든다.
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다.
public static class SwordWaveBuilder
{
    private const string PrefabFolder = "Assets/03.Prefabs/Weapons/SwordWaves";
    private const string MaterialFolder = "Assets/09.Materials/Weapons/SwordWaves";
    private const string PoolsRootName = "Pools";
    private const string WavePoolsName = "SwordWavePools";
    private const float GreenHue = 0.33f;
    private const float MinSaturation = 0.15f;   // 이보다 흰색/회색에 가까운 색은 그대로 둔다.
    private const float WaveSpeed = 70f;
    private const float WaveScale = 3f;

    // 검기로 쓸 원본. 이름에 slash가 들어간 Hovl 이펙트(Sparks 제외)를 모두 모았다.
    private static readonly string[] SourcePrefabs =
    {
        "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash blue.prefab",
        "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash purple.prefab",
        "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash red.prefab",
        "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Electro slash.prefab",
        "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Snow slash.prefab",
        "Assets/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Stone slash.prefab",
        "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/AoE slash blue.prefab",
        "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/AoE slash green.prefab",
        "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/AoE slash orange.prefab",
    };

    [MenuItem("Tools/Weapons/Build Sword Waves")]
    private static void Build()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        var prefabs = new List<GameObject>();
        foreach (string source in SourcePrefabs)
        {
            GameObject prefab = BuildGreenPrefab(source);
            if (prefab != null)
            {
                prefabs.Add(prefab);
            }
        }

        DeleteStalePrefabs(prefabs);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (prefabs.Count == 0)
        {
            Debug.LogError("검기 프리팹을 하나도 만들지 못했습니다.");
            return;
        }

        Undo.SetCurrentGroupName("Build Sword Waves");
        int undoGroup = Undo.GetCurrentGroup();

        EffectPool[] pools = BuildPools(prefabs);
        AssignToGun(pools);

        EditorSceneManager.MarkAllScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"초록 검기 {prefabs.Count}종을 만들고 총에 연결했습니다. 씬을 저장하세요.");
    }

    // 목록에서 빠진 원본의 예전 복사본을 지운다.
    private static void DeleteStalePrefabs(List<GameObject> current)
    {
        var keep = new HashSet<string>();
        foreach (GameObject prefab in current)
        {
            keep.Add(AssetDatabase.GetAssetPath(prefab));
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!keep.Contains(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static GameObject BuildGreenPrefab(string sourcePath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) == null)
        {
            Debug.LogWarning($"검기 원본을 찾지 못했습니다: {sourcePath}");
            return null;
        }

        string baseName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
        string prefabPath = $"{PrefabFolder}/{baseName} (Green).prefab";

        // 원본에서 새로 복사해 항상 같은 결과가 나오게 한다.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        if (!AssetDatabase.CopyAsset(sourcePath, prefabPath))
        {
            Debug.LogWarning($"프리팹을 복사하지 못했습니다: {sourcePath}");
            return null;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            root.name = $"{baseName} (Green)";
            root.transform.localScale = Vector3.one * WaveScale;
            var clonedMaterials = new Dictionary<Material, Material>();

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    shared[i] = GetGreenMaterial(shared[i], baseName, clonedMaterials);
                }

                renderer.sharedMaterials = shared;
            }

            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                GreenParticleSystem(system);
            }

            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                light.color = ToGreen(light.color);
            }

            // 날아가는 동안 효과가 끊기지 않도록 반복 재생한다. 맞거나 사거리 끝에서 풀로 돌아간다.
            if (root.GetComponent<PooledObject>() == null)
            {
                root.AddComponent<PooledObject>();
            }

            Bullet wave = root.GetComponent<Bullet>();
            if (wave == null)
            {
                wave = root.AddComponent<Bullet>();
            }

            var serialized = new SerializedObject(wave);
            serialized.FindProperty("_speed").floatValue = WaveSpeed;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 원본 재질은 그대로 두고, 색 속성을 초록으로 바꾼 복사본을 쓴다. 같은 재질은 한 번만 복사한다.
    private static Material GetGreenMaterial(Material source, string prefix, Dictionary<Material, Material> cache)
    {
        if (source == null)
        {
            return null;
        }

        if (cache.TryGetValue(source, out Material existing))
        {
            return existing;
        }

        string path = $"{MaterialFolder}/{prefix}_{source.name}_Green.mat";
        Material green = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (green == null)
        {
            green = new Material(source);
            AssetDatabase.CreateAsset(green, path);
        }
        else
        {
            green.CopyPropertiesFromMaterial(source);
        }

        Shader shader = source.shader;
        int propertyCount = shader.GetPropertyCount();
        for (int i = 0; i < propertyCount; i++)
        {
            if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Color)
            {
                continue;
            }

            int id = shader.GetPropertyNameId(i);
            green.SetColor(id, ToGreen(source.GetColor(id)));
        }

        EditorUtility.SetDirty(green);
        cache[source] = green;
        return green;
    }

    private static void GreenParticleSystem(ParticleSystem system)
    {
        ParticleSystem.MainModule main = system.main;
        main.startColor = ToGreen(main.startColor);
        main.loop = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        if (colorOverLifetime.enabled)
        {
            colorOverLifetime.color = ToGreen(colorOverLifetime.color);
        }

        ParticleSystem.ColorBySpeedModule colorBySpeed = system.colorBySpeed;
        if (colorBySpeed.enabled)
        {
            colorBySpeed.color = ToGreen(colorBySpeed.color);
        }

        ParticleSystem.TrailModule trails = system.trails;
        if (trails.enabled)
        {
            trails.colorOverLifetime = ToGreen(trails.colorOverLifetime);
            trails.colorOverTrail = ToGreen(trails.colorOverTrail);
        }
    }

    private static ParticleSystem.MinMaxGradient ToGreen(ParticleSystem.MinMaxGradient value)
    {
        switch (value.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(ToGreen(value.color));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(ToGreen(value.colorMin), ToGreen(value.colorMax));
            case ParticleSystemGradientMode.Gradient:
            case ParticleSystemGradientMode.RandomColor:
                return new ParticleSystem.MinMaxGradient(ToGreen(value.gradient)) { mode = value.mode };
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(ToGreen(value.gradientMin), ToGreen(value.gradientMax));
            default:
                return value;
        }
    }

    private static Gradient ToGreen(Gradient source)
    {
        var result = new Gradient { mode = source.mode, alphaKeys = source.alphaKeys };
        GradientColorKey[] keys = source.colorKeys;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].color = ToGreen(keys[i].color);
        }

        result.colorKeys = keys;
        return result;
    }

    // 색조만 초록으로 돌린다. 밝기(HDR 포함)와 알파는 유지하고, 흰색·회색·검정은 그대로 둔다.
    private static Color ToGreen(Color color)
    {
        Color.RGBToHSV(color, out _, out float saturation, out float value);
        if (saturation < MinSaturation)
        {
            return color;
        }

        Color green = Color.HSVToRGB(GreenHue, saturation, value, true);
        green.a = color.a;
        return green;
    }

    private static EffectPool[] BuildPools(List<GameObject> prefabs)
    {
        GameObject poolsRoot = GameObject.Find(PoolsRootName);
        if (poolsRoot == null)
        {
            poolsRoot = new GameObject(PoolsRootName);
            Undo.RegisterCreatedObjectUndo(poolsRoot, "Build Sword Waves");
        }

        Transform wavePools = poolsRoot.transform.Find(WavePoolsName);
        if (wavePools == null)
        {
            var holder = new GameObject(WavePoolsName);
            Undo.RegisterCreatedObjectUndo(holder, "Build Sword Waves");
            wavePools = holder.transform;
            wavePools.SetParent(poolsRoot.transform, false);
        }

        var poolNames = new HashSet<string>();
        foreach (GameObject prefab in prefabs)
        {
            poolNames.Add($"{prefab.name} Pool");
        }

        for (int i = wavePools.childCount - 1; i >= 0; i--)
        {
            if (!poolNames.Contains(wavePools.GetChild(i).name))
            {
                Undo.DestroyObjectImmediate(wavePools.GetChild(i).gameObject);
            }
        }

        var pools = new EffectPool[prefabs.Count];
        for (int i = 0; i < prefabs.Count; i++)
        {
            string poolName = $"{prefabs[i].name} Pool";
            Transform poolTransform = wavePools.Find(poolName);
            if (poolTransform == null)
            {
                var poolObject = new GameObject(poolName);
                Undo.RegisterCreatedObjectUndo(poolObject, "Build Sword Waves");
                poolTransform = poolObject.transform;
                poolTransform.SetParent(wavePools, false);
            }

            EffectPool pool = poolTransform.GetComponent<EffectPool>();
            if (pool == null)
            {
                pool = Undo.AddComponent<EffectPool>(poolTransform.gameObject);
            }

            var serialized = new SerializedObject(pool);
            serialized.FindProperty("_prefab").objectReferenceValue = prefabs[i];
            serialized.FindProperty("_defaultCapacity").intValue = 6;
            serialized.FindProperty("_maxSize").intValue = 30;
            serialized.FindProperty("_lifetime").floatValue = 0f; // 검기가 맞거나 사거리 끝에서 스스로 돌아간다.
            serialized.ApplyModifiedProperties();
            pools[i] = pool;
        }

        return pools;
    }

    private static void AssignToGun(EffectPool[] pools)
    {
        PlayerGunFire gun = Object.FindFirstObjectByType<PlayerGunFire>();
        if (gun == null)
        {
            Debug.LogWarning("씬에서 PlayerGunFire를 찾지 못해 검기 풀을 연결하지 않았습니다.");
            return;
        }

        var serialized = new SerializedObject(gun);
        SerializedProperty array = serialized.FindProperty("_swordWavePools");
        array.arraySize = pools.Length;
        for (int i = 0; i < pools.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = pools[i];
        }

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
