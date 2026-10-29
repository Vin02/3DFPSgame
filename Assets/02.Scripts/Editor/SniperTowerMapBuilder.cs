using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 맵을 "높은 탑 하나 + 사방에서 오는 적"으로 바꾼다.
//  - 숲의 바위·덤불·등불을 지우고, 씬에 놓인 달걀·드럼통은 꺼서 땅 위에 탑 말고는 아무것도 없게 한다. (땅·바깥 벽·조명은 그대로)
//  - 땅 한가운데에 탑(몸통 + 꼭대기 발판 + 난간)을 세우고 플레이어를 꼭대기에 올린다.
//  - 탑 둘레에 부딪힘 구역(TowerCrashZone)을 둬서, 닿은 좀비는 사라지고 플레이어가 피해를 입는다.
//  - 맵 곳곳에 가로등을 많이 세우고, 달빛·환경광·안개·노출을 밝게 올린다.
//  - 좀비 스포너를 탑 둘레 원에 고르게 둬서 사방에서 오게 하고, 좀비가 멀리서도 탑을 향해 몰려오게 한다.
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다. (Ctrl+Z로 되돌릴 수 있다)
public static class SniperTowerMapBuilder
{
    private const string UndoName = "Build Sniper Tower";
    private const string TowerRootName = "Tower";
    private const string MaterialFolder = "Assets/09.Materials/Forest";

    // false면 탑·부딪힘 구역·꼭대기 등을 만들지 않고 플레이어를 땅 위에 둔다. (지금 설정: 땅에서 싸운다)
    private const bool WithTower = false;

    private const float TowerHeight = 20f;
    private const float BodyRadius = 3.5f;
    private const float PlatformRadius = 6f;
    private const float PlatformThickness = 0.5f;
    private const int RailSegments = 32;
    private const float RailRadius = PlatformRadius - 0.3f;
    private const float RailHeight = 1.2f;
    private const float RailColliderHeight = 4f;   // 점프로 넘어가 떨어지지 않도록 보이는 난간보다 높게 막는다.

    private const float SpawnRingRadius = 50f;
    private const float SpawnRingJitter = 6f;
    private const float SpawnHeight = 4f;          // 좀비 몸 중심 높이(좀비 크기 3.5 + 여유). 땅에 떨어지며 나타난다.
    private const float ZombieDetectDistance = 150f; // 스폰 지점에서 탑이 보이지 않아도 곧장 몰려온다.
    private const float ZombieComebackDistance = 220f;

    private const float CrashZoneMargin = 1.3f;      // 탑 벽에서 이만큼 떨어진 곳부터 부딪힘으로 친다. (좀비 몸이 벽에 막히는 거리보다 크게)
    private const float CrashDamage = 10f;

    // 가로등: 고리 모양으로 안쪽부터 바깥까지 (반지름, 개수). 스포너 고리(약 50m) 둘레와 그 안쪽을 밝힌다.
    private static readonly (float radius, int count)[] LampRings = { (16f, 6), (30f, 10), (44f, 14), (58f, 16) };
    private const float LampPoleHeight = 7f;
    private const float LampRange = 36f;
    private const float LampIntensity = 150f;   // URP 점광원은 거리 제곱에 반비례해 약해지고, 땅 색이 어두워서 값이 크다.
    private const int PlatformLampCount = 6;
    private const string LampRootName = "Lamps";
    private const string VolumeProfilePath = "Assets/Settings/HorrorForestVolume.asset";

    private const int IgnoreRaycastLayer = 2;

    private static readonly string[] ZombiePrefabPaths =
    {
        "Assets/03.Prefabs/Monsters/Zombie_Enemy_1.prefab",
        "Assets/03.Prefabs/Monsters/Zombie_Enemy_2.prefab",
        "Assets/03.Prefabs/Monsters/Zombie_Enemy_3.prefab",
    };

    [MenuItem("Tools/Map/Build Sniper Tower")]
    private static void Build()
    {
        GameObject player = GameObject.Find("Player");
        ZombieManager zombieManager = Object.FindFirstObjectByType<ZombieManager>();
        if (player == null || zombieManager == null)
        {
            Debug.LogError("Player 또는 ZombieManager를 찾지 못했습니다.");
            return;
        }

        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();

        ClearProps();
        if (WithTower)
        {
            BuildTower();
            AddCrashZone(player.GetComponent<PlayerHealth>());
        }
        else
        {
            RemoveTower();
        }

        PlacePlayer(player);
        BuildLamps();
        BrightenScene();
        PlaceSpawners(zombieManager);
        ApplyZombieChase();

        EditorSceneManager.MarkAllScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        Debug.Log("저격 탑 맵을 만들었습니다. 결과를 확인한 뒤 씬을 저장하세요.");
    }

    // ───────────────────────── 정리 ─────────────────────────

    private static void ClearProps()
    {
        GameObject forest = FindRoot("Forest");
        if (forest != null)
        {
            foreach (string group in new[] { "Rocks", "Bushes", "Lanterns" })
            {
                Transform target = forest.transform.Find(group);
                if (target != null)
                {
                    Undo.DestroyObjectImmediate(target.gameObject);
                }
            }
        }

        // 달걀·드럼통은 지우지 않고 꺼 둔다. (되살리려면 다시 켜면 된다)
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            bool prop = root.name.StartsWith("Egg") || root.name == "Drum";
            if (prop && root.activeSelf)
            {
                Undo.RecordObject(root, UndoName);
                root.SetActive(false);
            }
        }
    }

    // ───────────────────────── 탑 ─────────────────────────

    private static void RemoveTower()
    {
        GameObject tower = FindRoot(TowerRootName);
        if (tower != null)
        {
            Undo.DestroyObjectImmediate(tower);
        }
    }

    private static void BuildTower()
    {
        GameObject old = FindRoot(TowerRootName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }

        var root = new GameObject(TowerRootName);
        Undo.RegisterCreatedObjectUndo(root, UndoName);
        root.isStatic = true;

        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");   // 반지름 0.5, 높이 2
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Material stone = LoadOrCreateMaterial("TowerStoneMat", new Color(0.2f, 0.2f, 0.22f), 0.15f);
        Material iron = LoadOrCreateMaterial("TowerIronMat", new Color(0.07f, 0.07f, 0.08f), 0.4f);

        // 몸통: 땅에서 발판 밑면까지
        float bodyHeight = TowerHeight - PlatformThickness;
        GameObject body = CreatePart(root.transform, "Body", cylinder, stone,
            new Vector3(0f, bodyHeight * 0.5f, 0f), Quaternion.identity, new Vector3(BodyRadius * 2f, bodyHeight * 0.5f, BodyRadius * 2f));
        body.AddComponent<MeshCollider>().sharedMesh = cylinder;

        // 꼭대기 발판 (윗면이 TowerHeight)
        GameObject platform = CreatePart(root.transform, "Platform", cylinder, stone,
            new Vector3(0f, TowerHeight - PlatformThickness * 0.5f, 0f), Quaternion.identity,
            new Vector3(PlatformRadius * 2f, PlatformThickness * 0.5f, PlatformRadius * 2f));
        platform.AddComponent<MeshCollider>().sharedMesh = cylinder;

        BuildRailing(root.transform, cube, iron);
    }

    // 난간: 보이는 부분(기둥·가로대)은 충돌이 없고, 따로 둔 투명 벽이 플레이어를 막는다.
    // 투명 벽은 Ignore Raycast 레이어라 조준·총알은 통과하므로 난간에 가려 쏘지 못하는 일이 없다.
    private static void BuildRailing(Transform tower, Mesh cube, Material iron)
    {
        var railingObject = new GameObject("Railing");
        railingObject.transform.SetParent(tower, false);
        railingObject.isStatic = true;
        Transform railing = railingObject.transform;

        float segment = 2f * RailRadius * Mathf.Sin(Mathf.PI / RailSegments);
        for (int i = 0; i < RailSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / RailSegments;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 post = radial * RailRadius + Vector3.up * TowerHeight;

            CreatePart(railing, "Post", cube, iron, post + Vector3.up * (RailHeight * 0.5f), Quaternion.identity,
                new Vector3(0.12f, RailHeight, 0.12f));

            // 이 기둥에서 다음 기둥까지의 가로대(위·중간)
            float nextAngle = (i + 1) * Mathf.PI * 2f / RailSegments;
            Vector3 nextRadial = new Vector3(Mathf.Cos(nextAngle), 0f, Mathf.Sin(nextAngle));
            Vector3 mid = (radial + nextRadial) * 0.5f * RailRadius;
            Quaternion rotation = Quaternion.LookRotation((nextRadial - radial).normalized);
            foreach (float height in new[] { RailHeight, RailHeight * 0.5f })
            {
                CreatePart(railing, "Bar", cube, iron, mid + Vector3.up * (TowerHeight + height), rotation,
                    new Vector3(0.07f, 0.07f, segment));
            }

            var wall = new GameObject("RailCollider");
            wall.transform.SetParent(railing, false);
            wall.layer = IgnoreRaycastLayer;
            wall.transform.SetPositionAndRotation(mid + Vector3.up * (TowerHeight + RailColliderHeight * 0.5f), rotation);
            var box = wall.AddComponent<BoxCollider>();
            box.size = new Vector3(0.4f, RailColliderHeight, segment * 1.1f);
        }
    }

    private static GameObject CreatePart(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.SetPositionAndRotation(position, rotation);
        part.transform.localScale = scale;
        part.isStatic = true;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    private static Material LoadOrCreateMaterial(string materialName, Color color, float smoothness)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/09.Materials", "Forest");
        }

        string path = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ───────────────────────── 부딪힘 구역 ─────────────────────────

    private static void AddCrashZone(PlayerHealth playerHealth)
    {
        GameObject tower = FindRoot(TowerRootName);
        if (tower == null || playerHealth == null)
        {
            Debug.LogError("탑 또는 PlayerHealth를 찾지 못해 부딪힘 구역을 만들지 않았습니다.");
            return;
        }

        // 플레이어가 선 발판(탑 꼭대기 위)에는 닿지 않도록 발판 아래까지만 덮는다.
        float height = TowerHeight - 1f;
        var zone = new GameObject("CrashZone");
        zone.transform.SetParent(tower.transform, false);
        zone.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

        var capsule = zone.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.radius = BodyRadius + CrashZoneMargin;
        capsule.height = height;

        var crashZone = zone.AddComponent<TowerCrashZone>();
        var serialized = new SerializedObject(crashZone);
        serialized.FindProperty("_playerHealth").objectReferenceValue = playerHealth;
        serialized.FindProperty("_damagePerZombie").floatValue = CrashDamage;
        serialized.ApplyModifiedProperties();
    }

    // ───────────────────────── 가로등 ─────────────────────────

    private static void BuildLamps()
    {
        GameObject old = FindRoot(LampRootName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }

        var root = new GameObject(LampRootName);
        Undo.RegisterCreatedObjectUndo(root, UndoName);

        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
        Mesh sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        Material iron = LoadOrCreateMaterial("TowerIronMat", new Color(0.07f, 0.07f, 0.08f), 0.4f);
        Material glow = LoadOrCreateGlowMaterial("LampGlowMat", new Color(1f, 0.78f, 0.45f));

        // 맵 곳곳의 가로등: 고리마다 고르게 놓되 조금씩 흩어 자연스럽게
        var random = new System.Random(77);
        foreach ((float radius, int count) in LampRings)
        {
            float phase = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float angle = phase + i * Mathf.PI * 2f / count + ((float)random.NextDouble() - 0.5f) * 0.25f;
                float r = radius + ((float)random.NextDouble() - 0.5f) * 5f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                CreateLamp(root.transform, position, LampPoleHeight, LampRange, LampIntensity, cylinder, sphere, iron, glow);
            }
        }

        // 탑 꼭대기: 난간 안쪽에 낮은 등을 둘러, 플레이어 주변이 어둡지 않게 한다.
        for (int i = 0; WithTower && i < PlatformLampCount; i++)
        {
            float angle = i * Mathf.PI * 2f / PlatformLampCount;
            Vector3 position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (PlatformRadius - 0.9f) + Vector3.up * TowerHeight;
            CreateLamp(root.transform, position, 2.4f, 18f, 14f, cylinder, sphere, iron, glow);
        }
    }

    private static void CreateLamp(Transform parent, Vector3 basePosition, float poleHeight, float range, float intensity,
        Mesh cylinder, Mesh sphere, Material iron, Material glow)
    {
        var lamp = new GameObject("Lamp");
        lamp.transform.SetParent(parent, false);
        lamp.transform.position = basePosition;
        lamp.isStatic = true;

        CreatePart(lamp.transform, "Pole", cylinder, iron, basePosition + Vector3.up * (poleHeight * 0.5f), Quaternion.identity,
            new Vector3(0.16f, poleHeight * 0.5f, 0.16f));
        GameObject bulb = CreatePart(lamp.transform, "Bulb", sphere, glow, basePosition + Vector3.up * (poleHeight + 0.2f), Quaternion.identity,
            Vector3.one * 0.5f);
        bulb.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        var lightObject = new GameObject("Light");
        lightObject.transform.SetParent(lamp.transform, false);
        lightObject.transform.position = basePosition + Vector3.up * (poleHeight + 0.2f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.78f, 0.5f);
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;   // 등이 많으므로 그림자는 끈다.
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }

    private static Material LoadOrCreateGlowMaterial(string materialName, Color color)
    {
        Material material = LoadOrCreateMaterial(materialName, new Color(0.1f, 0.08f, 0.05f), 0.2f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 4f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ───────────────────────── 밝기 ─────────────────────────

    // 달빛·환경광을 올리고 안개를 옅게 해서 멀리까지 보이게 한다. 노출도 조금 올린다.
    private static void BrightenScene()
    {
        GameObject sunObject = FindRoot("Directional Light");
        if (sunObject != null)
        {
            Light moon = sunObject.GetComponent<Light>();
            Undo.RecordObject(moon, UndoName);
            moon.intensity = 2.0f;
        }

        RenderSettings.ambientSkyColor = new Color(0.3f, 0.35f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.2f, 0.23f, 0.3f);
        RenderSettings.ambientGroundColor = new Color(0.07f, 0.07f, 0.08f);
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.08f);
        DynamicGI.UpdateEnvironment();

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile != null && profile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments color))
        {
            color.postExposure.Override(1.8f);
            EditorUtility.SetDirty(profile);
        }
    }

    // ───────────────────────── 플레이어·좀비 ─────────────────────────

    private static void PlacePlayer(GameObject player)
    {
        // 캐릭터 컨트롤러가 켜져 있으면 위치를 직접 바꿔도 되돌려 놓으므로 잠깐 끈다. 발이 발판 윗면에 닿게 둔다.
        var controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null)
        {
            controller.enabled = false;
        }

        Undo.RecordObject(player.transform, UndoName);
        float halfHeight = controller != null ? controller.height * 0.5f : 1f;
        player.transform.position = new Vector3(0f, (WithTower ? TowerHeight : 0f) + halfHeight + 0.05f, 0f);

        if (controller != null)
        {
            controller.enabled = wasEnabled;
        }
    }

    // 스포너를 탑 둘레 원에 고르게 놓는다. 이미 있는 스포너를 재사용하므로 ZombieManager의 연결은 그대로다.
    private static void PlaceSpawners(ZombieManager manager)
    {
        Transform parent = manager.transform.Find("Spawners");
        if (parent == null || parent.childCount == 0)
        {
            Debug.LogWarning("ZombieManager 아래에 Spawners가 없어 스포너를 옮기지 않았습니다.");
            return;
        }

        int count = parent.childCount;
        var random = new System.Random(2024);
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            float radius = SpawnRingRadius + ((float)random.NextDouble() * 2f - 1f) * SpawnRingJitter;

            Transform spawner = parent.GetChild(i);
            Undo.RecordObject(spawner, UndoName);
            spawner.position = new Vector3(Mathf.Cos(angle) * radius, SpawnHeight, Mathf.Sin(angle) * radius);
        }
    }

    // 좀비가 스폰 지점에서 탑이 한참 멀어도 감지하고 몰려오게 한다.
    private static void ApplyZombieChase()
    {
        foreach (string path in ZombiePrefabPaths)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            SetChase(prefab.GetComponent<Zombie>(), false);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        foreach (Zombie zombie in Object.FindObjectsByType<Zombie>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SetChase(zombie, true);
        }
    }

    private static void SetChase(Zombie zombie, bool recordUndo)
    {
        if (zombie == null)
        {
            return;
        }

        var serialized = new SerializedObject(zombie);
        serialized.FindProperty("_detectDistance").floatValue = ZombieDetectDistance;
        serialized.FindProperty("_comebackDistance").floatValue = ZombieComebackDistance;
        if (recordUndo)
        {
            serialized.ApplyModifiedProperties();
        }
        else
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static GameObject FindRoot(string name)
    {
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return null;
    }
}
