using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// 맵을 어두운 숲으로 바꾸고 좀비 척결 공포 게임에 맞게 조명·후처리·HUD·좀비 설정을 한 번에 만든다.
//  - Warehouse를 지우고, 테스트 코스(Map)는 숲과 어울리지 않아 끈다.
//  - 나무·고목·바위·덤불·등불을 고정 시드로 흩뿌리고, 바깥으로 못 나가게 보이지 않는 벽을 세운다.
//  - 좀비 스포너를 숲 곳곳에 두고, 좀비 크기와 체력을 키운다.
//  - 달빛·안개·손전등·라이트 프로브·베이크 설정·공포 분위기 후처리 볼륨을 만든다.
//  - 위쪽에 좀비 수·처치 수를 표시하는 HUD를 만든다. (탄약·폭탄은 WeaponHud가 맡는다) (좀비는 끝없이 나온다)
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다. (씬 오브젝트 변경은 Ctrl+Z로 되돌릴 수 있다)
// 라이트맵 베이크는 오래 걸리므로 Tools > Map > Bake Horror Forest Lighting으로 따로 실행한다.
public static class HorrorForestBuilder
{
    private const string UndoName = "Build Horror Forest";
    private const string ForestRootName = "Forest";
    private const string MeshFolder = "Assets/05.Models/Forest";
    private const string MaterialFolder = "Assets/09.Materials/Forest";
    private const string VolumeProfilePath = "Assets/Settings/HorrorForestVolume.asset";
    private const string LightingSettingsPath = "Assets/Settings/HorrorForestLighting.lighting";
    private const string SkyMaterialName = "NightSkyMat";

    private static readonly string[] ZombiePrefabPaths =
    {
        "Assets/03.Prefabs/Monsters/Zombie_Enemy_1.prefab",
        "Assets/03.Prefabs/Monsters/Zombie_Enemy_2.prefab",
        "Assets/03.Prefabs/Monsters/Zombie_Enemy_3.prefab",
    };

    private const int Seed = 1337;
    private const float HalfSize = 75f;          // 숲 영역 반 크기 (땅은 160m x 160m)
    private const float ClearRadius = 9f;        // 시작 지점 주변 빈 공간
    private const int TreeCount = 0;
    private const int EdgeTreeCount = 0;        // 가장자리를 막는 빽빽한 나무
    private const int DeadTreeCount = 0;
    private const int RockCount = 60;
    private const int BushCount = 160;
    private const int LanternCount = 8;
    private const int SpawnerCount = 18;

    // 코스믹 호러: 나무 사이를 걸어 다니는 거대하고 기괴한 존재. 겉모습은 GrotesqueZombieBuilder가 만든다.
    private const float ZombieScale = 3.5f;
    private const float ZombieHealth = 300f;
    private const float ZombieDamage = 20f;
    private const float ZombieMoveSpeed = 4f;
    private const float ZombieAttackRange = 2.5f;          // 몸 표면에서 이 거리 안이면 팔이 닿는다 (월드 단위)
    private const float ZombieColliderRadius = 0.2f;       // 로컬 반지름. 크기 배율을 곱하면 0.7m라 나무 사이를 지나갈 수 있다.

    // 숲이 어둡고 넓어서 멀리서도 플레이어를 찾아오게 한다. 한 번 쫓기 시작하면 거의 포기하지 않는다.
    private const float ZombieDetectDistance = 40f;
    private const float ZombieComebackDistance = 90f;

    private static readonly Color FogColor = new Color(0.022f, 0.028f, 0.032f);

    private struct Part
    {
        public Mesh Mesh;
        public Matrix4x4 Matrix;

        public Part(Mesh mesh, Vector3 position, Vector3 euler, Vector3 scale)
        {
            Mesh = mesh;
            Matrix = Matrix4x4.TRS(position, Quaternion.Euler(euler), scale);
        }
    }

    private struct Materials
    {
        public Material Bark;
        public Material Pine;
        public Material DeadWood;
        public Material Rock;
        public Material Bush;
        public Material Ground;
        public Material LanternGlow;
        public Material Iron;
    }

    private struct Meshes
    {
        public Mesh PineTall;
        public Mesh PineShort;
        public Mesh DeadTree;
        public Mesh Rock;
        public Mesh Bush;
        public Mesh LanternPost;
        public Mesh LanternGlass;
    }

    [MenuItem("Tools/Map/Build Horror Forest")]
    private static void Build()
    {
        GameObject player = GameObject.Find("Player");
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        ZombieManager zombieManager = Object.FindFirstObjectByType<ZombieManager>();
        Camera mainCamera = Camera.main;

        if (player == null || canvas == null || zombieManager == null || mainCamera == null)
        {
            Debug.LogError("Player, Canvas, ZombieManager, Main Camera 중 찾지 못한 것이 있습니다.");
            return;
        }

        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();

        RemoveOldMap();

        Materials materials = PrepareMaterials();
        Meshes meshes = PrepareMeshes();

        SetupGround(materials.Ground);
        List<Vector3> spawnerPoints = BuildForest(materials, meshes);
        SetupZombies(zombieManager, player.transform, spawnerPoints);
        SetupLighting(mainCamera);
        SetupPostProcessing();
        BuildCombatHud(canvas, zombieManager, player.GetComponent<PlayerGunFire>());

        EditorSceneManager.MarkAllScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();

        Debug.Log("공포 숲 맵을 만들었습니다. 결과를 확인한 뒤 씬을 저장하고, 필요하면 Tools > Map > Bake Horror Forest Lighting으로 베이크하세요.");
    }

    // 숲(과 베이크 결과)은 그대로 두고 좀비 능력치·생성 규칙과 전투 HUD만 다시 적용한다.
    [MenuItem("Tools/Map/Apply Zombie And Hud Settings")]
    private static void ApplyZombieAndHud()
    {
        GameObject player = GameObject.Find("Player");
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        ZombieManager zombieManager = Object.FindFirstObjectByType<ZombieManager>();
        if (player == null || canvas == null || zombieManager == null)
        {
            Debug.LogError("Player, Canvas, ZombieManager 중 찾지 못한 것이 있습니다.");
            return;
        }

        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();

        ApplyZombieStatsToAll();
        ApplySpawnRules(zombieManager, player.transform);
        BuildCombatHud(canvas, zombieManager, player.GetComponent<PlayerGunFire>());

        EditorSceneManager.MarkAllScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        Debug.Log("좀비 설정과 전투 HUD를 적용했습니다.");
    }

    [MenuItem("Tools/Map/Bake Horror Forest Lighting")]
    private static void Bake()
    {
        Lightmapping.lightingSettings = LoadOrCreateLightingSettings();
        if (!Lightmapping.BakeAsync())
        {
            Debug.LogError("라이트맵 베이크를 시작하지 못했습니다. 씬을 한 번 저장한 뒤 다시 시도하세요.");
            return;
        }

        Debug.Log("라이트맵 베이크를 시작했습니다. 진행 상황은 에디터 오른쪽 아래 진행 바에서 볼 수 있습니다.");
    }

    // ───────────────────────── 기존 맵 정리 ─────────────────────────

    private static void RemoveOldMap()
    {
        GameObject warehouse = FindRoot("Warehouse");
        if (warehouse != null)
        {
            Undo.DestroyObjectImmediate(warehouse);
        }

        // 테스트 코스(계단·경사로)는 숲과 어울리지 않아 지우지 않고 끈다.
        GameObject testCourse = FindRoot("Map");
        if (testCourse != null && testCourse.activeSelf)
        {
            Undo.RecordObject(testCourse, UndoName);
            testCourse.SetActive(false);
        }

        GameObject oldForest = FindRoot(ForestRootName);
        if (oldForest != null)
        {
            Undo.DestroyObjectImmediate(oldForest);
        }
    }

    // ───────────────────────── 재질 ─────────────────────────

    private static Materials PrepareMaterials()
    {
        return new Materials
        {
            // 재질은 실제 숲 정도의 반사율로 두고, 어둠은 조명과 안개로 만든다. (재질까지 어두우면 손전등을 비춰도 보이지 않는다)
            Bark = LoadOrCreateMaterial("ForestBarkMat", new Color(0.26f, 0.2f, 0.16f), 0.1f),
            Pine = LoadOrCreateMaterial("ForestPineMat", new Color(0.11f, 0.17f, 0.12f), 0.05f),
            DeadWood = LoadOrCreateMaterial("ForestDeadWoodMat", new Color(0.32f, 0.3f, 0.28f), 0.1f),
            Rock = LoadOrCreateMaterial("ForestRockMat", new Color(0.33f, 0.33f, 0.34f), 0.2f),
            Bush = LoadOrCreateMaterial("ForestBushMat", new Color(0.12f, 0.19f, 0.1f), 0.05f),
            Ground = LoadOrCreateMaterial("ForestGroundMat", new Color(0.19f, 0.16f, 0.12f), 0.05f),
            LanternGlow = LoadOrCreateMaterial("ForestLanternGlowMat", new Color(1f, 0.5f, 0.2f), 0.5f, new Color(1f, 0.42f, 0.12f) * 4f),
            Iron = LoadOrCreateMaterial("ForestIronMat", new Color(0.08f, 0.08f, 0.09f), 0.4f),
        };
    }

    private static Material LoadOrCreateMaterial(string materialName, Color color, float smoothness, Color? emission = null)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{materialName}.mat";

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.enableInstancing = true;

        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    // ───────────────────────── 메시 ─────────────────────────

    private static Meshes PrepareMeshes()
    {
        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Mesh cone = CreateCone(9);

        var meshes = new Meshes
        {
            // 서브메시 0 = 줄기, 1 = 잎
            PineTall = BuildMesh("PineTall",
                new[] { Trunk(cylinder, 0f, 9f, 0.28f) },
                new[]
                {
                    new Part(cone, new Vector3(0f, 2.2f, 0f), Vector3.zero, new Vector3(3f, 4.2f, 3f)),
                    new Part(cone, new Vector3(0f, 4.3f, 0f), new Vector3(0f, 25f, 0f), new Vector3(2.4f, 3.6f, 2.4f)),
                    new Part(cone, new Vector3(0f, 6.3f, 0f), new Vector3(0f, 50f, 0f), new Vector3(1.8f, 3.1f, 1.8f)),
                    new Part(cone, new Vector3(0f, 8.2f, 0f), new Vector3(0f, 10f, 0f), new Vector3(1.1f, 2.6f, 1.1f)),
                }),
            PineShort = BuildMesh("PineShort",
                new[] { Trunk(cylinder, 0f, 5f, 0.24f) },
                new[]
                {
                    new Part(cone, new Vector3(0f, 1.2f, 0f), Vector3.zero, new Vector3(2.6f, 3.4f, 2.6f)),
                    new Part(cone, new Vector3(0f, 3f, 0f), new Vector3(0f, 30f, 0f), new Vector3(1.9f, 2.8f, 1.9f)),
                    new Part(cone, new Vector3(0f, 4.6f, 0f), new Vector3(0f, 15f, 0f), new Vector3(1.1f, 2.2f, 1.1f)),
                }),
            DeadTree = BuildMesh("DeadTree",
                new[]
                {
                    Trunk(cylinder, 0f, 7.5f, 0.22f),
                    Branch(cylinder, new Vector3(0f, 4.2f, 0f), new Vector3(0f, 0f, 55f), 2.4f, 0.08f),
                    Branch(cylinder, new Vector3(0f, 5.3f, 0f), new Vector3(0f, 130f, -50f), 2f, 0.07f),
                    Branch(cylinder, new Vector3(0f, 3.1f, 0f), new Vector3(0f, 240f, 60f), 1.8f, 0.07f),
                    Branch(cylinder, new Vector3(0f, 6.4f, 0f), new Vector3(0f, 300f, 40f), 1.4f, 0.05f),
                }),
            Rock = BuildMesh("Rock",
                new[]
                {
                    new Part(sphere, new Vector3(0f, 0.25f, 0f), new Vector3(10f, 20f, 5f), new Vector3(2.2f, 1.2f, 1.7f)),
                    new Part(sphere, new Vector3(0.7f, 0.15f, 0.4f), new Vector3(-15f, 70f, 10f), new Vector3(1.2f, 0.8f, 1f)),
                }),
            Bush = BuildMesh("Bush",
                new[]
                {
                    new Part(sphere, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(1.5f, 1f, 1.4f)),
                    new Part(sphere, new Vector3(0.55f, 0.35f, 0.2f), Vector3.zero, new Vector3(1f, 0.8f, 1f)),
                    new Part(sphere, new Vector3(-0.45f, 0.3f, -0.3f), Vector3.zero, new Vector3(0.9f, 0.7f, 0.9f)),
                }),
            LanternPost = BuildMesh("LanternPost",
                new[]
                {
                    Trunk(cylinder, 0f, 2.2f, 0.06f),
                    new Part(cube, new Vector3(0f, 2.15f, 0f), Vector3.zero, new Vector3(0.32f, 0.06f, 0.32f)),
                    new Part(cube, new Vector3(0f, 2.65f, 0f), Vector3.zero, new Vector3(0.36f, 0.06f, 0.36f)),
                }),
            LanternGlass = BuildMesh("LanternGlass",
                new[] { new Part(cube, new Vector3(0f, 2.4f, 0f), Vector3.zero, new Vector3(0.24f, 0.44f, 0.24f)) }),
        };

        Object.DestroyImmediate(cone);
        return meshes;
    }

    // y0에서 시작하는 높이 h, 반지름 r 줄기. (기본 원기둥은 높이 2, 반지름 0.5)
    private static Part Trunk(Mesh cylinder, float y0, float height, float radius)
    {
        return new Part(cylinder, new Vector3(0f, y0 + height * 0.5f, 0f), Vector3.zero, new Vector3(radius * 2f, height * 0.5f, radius * 2f));
    }

    // 줄기의 한 점에서 비스듬히 뻗은 가지. 원기둥 중심을 가지 길이의 절반만큼 밀어낸다.
    private static Part Branch(Mesh cylinder, Vector3 origin, Vector3 euler, float length, float radius)
    {
        Quaternion rotation = Quaternion.Euler(euler);
        Vector3 center = origin + rotation * Vector3.up * (length * 0.5f);
        return new Part(cylinder, center, euler, new Vector3(radius * 2f, length * 0.5f, radius * 2f));
    }

    // 밑면 반지름 1, 높이 1인 원뿔. (밑면 중심이 원점)
    private static Mesh CreateCone(int segments)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            float mid = (a0 + a1) * 0.5f;

            int start = vertices.Count;
            vertices.Add(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)));
            vertices.Add(new Vector3(0f, 1f, 0f));
            vertices.Add(new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)));
            normals.Add(new Vector3(Mathf.Cos(a0), 1f, Mathf.Sin(a0)).normalized);
            normals.Add(new Vector3(Mathf.Cos(mid), 1f, Mathf.Sin(mid)).normalized);
            normals.Add(new Vector3(Mathf.Cos(a1), 1f, Mathf.Sin(a1)).normalized);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);

            // 밑면
            int bottom = vertices.Count;
            vertices.Add(Vector3.zero);
            vertices.Add(new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)));
            vertices.Add(new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)));
            normals.Add(Vector3.down);
            normals.Add(Vector3.down);
            normals.Add(Vector3.down);
            triangles.Add(bottom);
            triangles.Add(bottom + 1);
            triangles.Add(bottom + 2);
        }

        var mesh = new Mesh { name = "Cone" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // 인자로 받은 부품 묶음 하나가 서브메시 하나가 된다.
    private static Mesh BuildMesh(string meshName, params Part[][] submeshes)
    {
        var merged = new CombineInstance[submeshes.Length];
        var temporary = new List<Mesh>();

        for (int s = 0; s < submeshes.Length; s++)
        {
            Part[] parts = submeshes[s];
            var combine = new CombineInstance[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                combine[i] = new CombineInstance { mesh = parts[i].Mesh, transform = parts[i].Matrix };
            }

            var sub = new Mesh { indexFormat = IndexFormat.UInt32 };
            sub.CombineMeshes(combine, true, true);
            temporary.Add(sub);
            merged[s] = new CombineInstance { mesh = sub, transform = Matrix4x4.identity };
        }

        var mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
        mesh.CombineMeshes(merged, false, true);
        mesh.RecalculateBounds();

        foreach (Mesh sub in temporary)
        {
            Object.DestroyImmediate(sub);
        }

        return SaveMesh(mesh);
    }

    private static Mesh SaveMesh(Mesh mesh)
    {
        EnsureFolder(MeshFolder);
        string path = $"{MeshFolder}/{mesh.name}.asset";

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        existing.Clear();
        EditorUtility.CopySerialized(mesh, existing);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // ───────────────────────── 땅과 숲 ─────────────────────────

    private static void SetupGround(Material groundMaterial)
    {
        GameObject ground = FindRoot("Ground");
        if (ground == null)
        {
            Debug.LogWarning("Ground를 찾지 못해 땅은 바꾸지 않았습니다.");
            return;
        }

        Undo.RecordObject(ground.transform, UndoName);
        ground.transform.localScale = new Vector3(HalfSize * 2f / 10f + 1f, 1f, HalfSize * 2f / 10f + 1f);

        var renderer = ground.GetComponent<MeshRenderer>();
        Undo.RecordObject(renderer, UndoName);
        renderer.sharedMaterial = groundMaterial;

        GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
    }

    // 숲을 만들고, 좀비 스포너를 둘 빈 자리를 돌려준다.
    private static List<Vector3> BuildForest(Materials materials, Meshes meshes)
    {
        var random = new System.Random(Seed);
        var occupied = new List<Vector4>(); // xyz = 위치, w = 차지하는 반지름

        GameObject root = new GameObject(ForestRootName);
        Undo.RegisterCreatedObjectUndo(root, UndoName);

        // 스포너 자리를 먼저 비워 둔다. (좀비가 나무 속에서 생성되지 않도록)
        var spawnerPoints = new List<Vector3>();
        for (int attempt = 0; attempt < 2000 && spawnerPoints.Count < SpawnerCount; attempt++)
        {
            Vector3 point = RandomPoint(random, HalfSize - 14f);
            float distance = new Vector2(point.x, point.z).magnitude;
            if (distance >= 20f && IsFree(occupied, point, 5f))
            {
                occupied.Add(new Vector4(point.x, 0f, point.z, 5f));
                spawnerPoints.Add(point);
            }
        }

        Transform trees = Group(root.transform, "Trees");
        Transform deadTrees = Group(root.transform, "DeadTrees");
        Transform rocks = Group(root.transform, "Rocks");
        Transform bushes = Group(root.transform, "Bushes");
        Transform lanterns = Group(root.transform, "Lanterns");

        Material[] pineMaterials = { materials.Bark, materials.Pine };

        // 가장자리를 빽빽하게 막는다.
        Scatter(random, occupied, EdgeTreeCount, 2.2f, () => RandomEdgePoint(random, HalfSize - 12f, HalfSize), position =>
            PlaceTree(trees, random.NextDouble() < 0.6 ? meshes.PineTall : meshes.PineShort, pineMaterials, position, random, 0.28f));

        Scatter(random, occupied, TreeCount, 3.2f, () => RandomPoint(random, HalfSize - 12f), position =>
            PlaceTree(trees, random.NextDouble() < 0.55 ? meshes.PineTall : meshes.PineShort, pineMaterials, position, random, 0.28f));

        Scatter(random, occupied, DeadTreeCount, 3f, () => RandomPoint(random, HalfSize - 6f), position =>
            PlaceTree(deadTrees, meshes.DeadTree, new[] { materials.DeadWood }, position, random, 0.22f));

        Scatter(random, occupied, RockCount, 2.5f, () => RandomPoint(random, HalfSize - 4f), position =>
            PlaceRock(rocks, meshes.Rock, materials.Rock, position, random));

        // 덤불은 몸을 막지 않으므로 겹치는 것만 피한다.
        Scatter(random, occupied, BushCount, 1.2f, () => RandomPoint(random, HalfSize - 2f), position =>
            PlaceProp(bushes, "Bush", meshes.Bush, new[] { materials.Bush }, position, RandomYaw(random), RandomScale(random, 0.7f, 1.4f)));

        Scatter(random, occupied, LanternCount, 3f, () => RandomRingPoint(random, 12f, 55f), position =>
            PlaceLantern(lanterns, meshes, materials, position, random));

        BuildBoundary(root.transform);
        BuildLightProbes(root.transform);

        return spawnerPoints;
    }

    private static void Scatter(System.Random random, List<Vector4> occupied, int count, float radius, System.Func<Vector3> pick, System.Action<Vector3> place)
    {
        int placed = 0;
        for (int attempt = 0; attempt < count * 30 && placed < count; attempt++)
        {
            Vector3 point = pick();
            if (new Vector2(point.x, point.z).magnitude < ClearRadius || !IsFree(occupied, point, radius))
            {
                continue;
            }

            occupied.Add(new Vector4(point.x, 0f, point.z, radius));
            place(point);
            placed++;
        }
    }

    private static bool IsFree(List<Vector4> occupied, Vector3 point, float radius)
    {
        foreach (Vector4 other in occupied)
        {
            float minDistance = Mathf.Max(radius, other.w);
            float dx = other.x - point.x;
            float dz = other.z - point.z;
            if (dx * dx + dz * dz < minDistance * minDistance)
            {
                return false;
            }
        }

        return true;
    }

    private static void PlaceTree(Transform parent, Mesh mesh, Material[] materials, Vector3 position, System.Random random, float trunkRadius)
    {
        float scale = RandomScale(random, 0.8f, 1.45f);
        GameObject tree = PlaceProp(parent, mesh.name, mesh, materials, position, RandomYaw(random), scale);

        // 몸통만 막는다. (잎은 지나갈 수 있게)
        var collider = tree.AddComponent<CapsuleCollider>();
        collider.radius = trunkRadius + 0.05f;
        collider.height = 6f;
        collider.center = new Vector3(0f, 3f, 0f);
    }

    private static void PlaceRock(Transform parent, Mesh mesh, Material material, Vector3 position, System.Random random)
    {
        GameObject rock = PlaceProp(parent, "Rock", mesh, new[] { material }, position, RandomYaw(random), RandomScale(random, 0.6f, 1.6f));
        var collider = rock.AddComponent<BoxCollider>();
        collider.center = mesh.bounds.center;
        collider.size = mesh.bounds.size * 0.85f;
    }

    private static GameObject PlaceProp(Transform parent, string name, Mesh mesh, Material[] materials, Vector3 position, float yaw, float scale)
    {
        var prop = new GameObject(name);
        prop.transform.SetParent(parent, false);
        prop.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        prop.transform.localScale = Vector3.one * scale;

        prop.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = prop.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        // 수백 개의 나무에 라이트맵을 주면 베이크가 너무 무거워서, 간접광은 라이트 프로브로 받는다.
        renderer.receiveGI = ReceiveGI.LightProbes;

        GameObjectUtility.SetStaticEditorFlags(prop, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic
            | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return prop;
    }

    private static void PlaceLantern(Transform parent, Meshes meshes, Materials materials, Vector3 position, System.Random random)
    {
        GameObject lantern = PlaceProp(parent, "Lantern", meshes.LanternPost, new[] { materials.Iron }, position, RandomYaw(random), 1f);
        lantern.AddComponent<CapsuleCollider>().radius = 0.12f;

        GameObject glass = PlaceProp(lantern.transform, "Glass", meshes.LanternGlass, new[] { materials.LanternGlow }, position, 0f, 1f);
        glass.transform.localPosition = Vector3.zero;
        glass.transform.localRotation = Quaternion.identity;
        glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        var lightObject = new GameObject("Light");
        lightObject.transform.SetParent(lantern.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.45f, 0.16f);
        light.range = 10f;
        light.intensity = 1.6f;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime; // 깜빡이므로 굽지 않는다.
        lightObject.AddComponent<LightFlicker>();
    }

    // 숲 밖으로 나가지 못하게 보이지 않는 벽을 세운다.
    private static void BuildBoundary(Transform root)
    {
        Transform boundary = Group(root, "Boundary");
        float edge = HalfSize + 2f;
        Vector3[] centers = { new Vector3(edge, 5f, 0f), new Vector3(-edge, 5f, 0f), new Vector3(0f, 5f, edge), new Vector3(0f, 5f, -edge) };
        Vector3[] sizes = { new Vector3(2f, 10f, edge * 2f), new Vector3(2f, 10f, edge * 2f), new Vector3(edge * 2f, 10f, 2f), new Vector3(edge * 2f, 10f, 2f) };

        for (int i = 0; i < centers.Length; i++)
        {
            var wall = new GameObject($"Wall_{i + 1}");
            wall.transform.SetParent(boundary, false);
            wall.transform.position = centers[i];
            wall.AddComponent<BoxCollider>().size = sizes[i];
        }
    }

    // 나무·바위·좀비가 베이크된 간접광을 받을 수 있게 숲 전체에 라이트 프로브를 깐다.
    private static void BuildLightProbes(Transform root)
    {
        var probes = new GameObject("LightProbes");
        probes.transform.SetParent(root, false);

        var positions = new List<Vector3>();
        const float step = 12f;
        for (float x = -HalfSize; x <= HalfSize + 0.01f; x += step)
        {
            for (float z = -HalfSize; z <= HalfSize + 0.01f; z += step)
            {
                positions.Add(new Vector3(x, 1f, z));
                positions.Add(new Vector3(x, 6f, z));
            }
        }

        probes.AddComponent<LightProbeGroup>().probePositions = positions.ToArray();
    }

    private static Vector3 RandomPoint(System.Random random, float half)
    {
        return new Vector3(Range(random, -half, half), 0f, Range(random, -half, half));
    }

    // 정사각형 테두리 띠(inner~outer) 안의 무작위 점
    private static Vector3 RandomEdgePoint(System.Random random, float inner, float outer)
    {
        float along = Range(random, -outer, outer);
        float across = Range(random, inner, outer) * (random.NextDouble() < 0.5 ? -1f : 1f);
        return random.NextDouble() < 0.5 ? new Vector3(along, 0f, across) : new Vector3(across, 0f, along);
    }

    private static Vector3 RandomRingPoint(System.Random random, float minRadius, float maxRadius)
    {
        float angle = Range(random, 0f, Mathf.PI * 2f);
        float radius = Range(random, minRadius, maxRadius);
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private static float RandomYaw(System.Random random) => Range(random, 0f, 360f);

    private static float RandomScale(System.Random random, float min, float max) => Range(random, min, max);

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    // ───────────────────────── 좀비 ─────────────────────────

    private static void SetupZombies(ZombieManager manager, Transform player, List<Vector3> spawnerPoints)
    {
        ApplyZombieStatsToAll();

        Transform spawnerParent = manager.transform.Find("Spawners");
        if (spawnerParent == null)
        {
            spawnerParent = new GameObject("Spawners").transform;
            Undo.RegisterCreatedObjectUndo(spawnerParent.gameObject, UndoName);
            Undo.SetTransformParent(spawnerParent, manager.transform, UndoName);
        }

        for (int i = spawnerParent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(spawnerParent.GetChild(i).gameObject);
        }

        var spawners = new ZombieSpawner[spawnerPoints.Count];
        for (int i = 0; i < spawnerPoints.Count; i++)
        {
            var spawnerObject = new GameObject($"Spawner_{i + 1}");
            Undo.RegisterCreatedObjectUndo(spawnerObject, UndoName);
            spawnerObject.transform.SetParent(spawnerParent, false);
            // 좀비의 몸 중심(키의 절반)보다 조금 위에서 생성해 땅에 떨어지게 한다.
            spawnerObject.transform.position = spawnerPoints[i] + Vector3.up * (ZombieScale + 0.5f);
            spawners[i] = spawnerObject.AddComponent<ZombieSpawner>();
        }

        var serialized = new SerializedObject(manager);
        SetObjectArray(serialized.FindProperty("_spawners"), spawners);
        serialized.ApplyModifiedProperties();

        ApplySpawnRules(manager, player);
    }

    private static void ApplyZombieStatsToAll()
    {
        foreach (string path in ZombiePrefabPaths)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            ApplyZombieStats(prefab, false);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        // 씬에 미리 놓은 좀비도 같은 크기와 체력으로 맞춘다.
        foreach (Zombie zombie in Object.FindObjectsByType<Zombie>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            ApplyZombieStats(zombie.gameObject, true);
        }
    }

    // 좀비는 끝없이 나온다. 동시에 살아 있는 수만 제한한다.
    private static void ApplySpawnRules(ZombieManager manager, Transform player)
    {
        // 좀비 몸 중심(키의 절반)보다 조금 위에서 생성해야 땅에 묻히지 않는다. 크기가 바뀌면 스포너 높이도 맞춘다.
        Transform spawnerParent = manager.transform.Find("Spawners");
        if (spawnerParent != null)
        {
            foreach (Transform spawner in spawnerParent)
            {
                Undo.RecordObject(spawner, UndoName);
                Vector3 position = spawner.position;
                position.y = ZombieScale + 0.5f;
                spawner.position = position;
            }
        }

        var serialized = new SerializedObject(manager);
        serialized.FindProperty("_player").objectReferenceValue = player;
        serialized.FindProperty("_minSpawnDistance").floatValue = 18f;
        serialized.FindProperty("_initialCount").intValue = 10;
        serialized.FindProperty("_maxAlive").intValue = 14;
        serialized.FindProperty("_spawnInterval").floatValue = 3f;
        serialized.ApplyModifiedProperties();
    }

    private static void ApplyZombieStats(GameObject zombieObject, bool recordUndo)
    {
        if (recordUndo)
        {
            Undo.RecordObject(zombieObject.transform, UndoName);
        }

        zombieObject.transform.localScale = Vector3.one * ZombieScale;

        // 씬에 미리 놓인 좀비는 커진 만큼 띄워서 땅에 묻히지 않게 한다. (몸 중심이 기준점)
        if (recordUndo)
        {
            Vector3 position = zombieObject.transform.position;
            position.y = Mathf.Max(position.y, ZombieScale + 0.5f);
            zombieObject.transform.position = position;
        }

        var controller = zombieObject.GetComponent<CharacterController>();
        if (controller != null)
        {
            if (recordUndo)
            {
                Undo.RecordObject(controller, UndoName);
            }

            controller.radius = ZombieColliderRadius;
        }

        var serialized = new SerializedObject(zombieObject.GetComponent<Zombie>());
        serialized.FindProperty("_maxHealth").floatValue = ZombieHealth;
        serialized.FindProperty("_detectDistance").floatValue = ZombieDetectDistance;
        serialized.FindProperty("_comebackDistance").floatValue = ZombieComebackDistance;
        serialized.FindProperty("_patrolRadius").floatValue = 10f;
        serialized.FindProperty("_attackDamage").floatValue = ZombieDamage;
        serialized.FindProperty("_moveSpeed").floatValue = ZombieMoveSpeed;
        serialized.FindProperty("_attackRange").floatValue = ZombieAttackRange;
        if (recordUndo)
        {
            serialized.ApplyModifiedProperties();
        }
        else
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        ApplyCosmicLook(zombieObject, recordUndo);
    }

    // 겉모습(얼굴 없는 살덩이 머리·눈·아가리·발톱·촉수·창백한 피부)은 GrotesqueZombieBuilder가 만든다.
    private static void ApplyCosmicLook(GameObject zombieObject, bool recordUndo)
    {
        GrotesqueZombieBuilder.ApplyTo(zombieObject, recordUndo);
    }

    // ───────────────────────── 조명 ─────────────────────────

    private static void SetupLighting(Camera mainCamera)
    {
        // 달빛: 차갑고 아주 어둡게. 베이크할 때 간접광만 굽고 그림자는 실시간으로 유지한다.
        GameObject sunObject = FindRoot("Directional Light");
        if (sunObject != null)
        {
            Light moon = sunObject.GetComponent<Light>();
            Undo.RecordObject(moon, UndoName);
            Undo.RecordObject(sunObject.transform, UndoName);
            moon.color = new Color(0.55f, 0.66f, 0.9f);
            moon.intensity = 0.3f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.9f;
            moon.lightmapBakeType = LightmapBakeType.Mixed;
            sunObject.transform.rotation = Quaternion.Euler(32f, 210f, 0f);
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.07f, 0.085f, 0.11f);
        RenderSettings.ambientEquatorColor = new Color(0.04f, 0.045f, 0.055f);
        RenderSettings.ambientGroundColor = new Color(0.015f, 0.015f, 0.015f);
        RenderSettings.reflectionIntensity = 0.15f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogDensity = 0.04f;
        RenderSettings.skybox = LoadOrCreateNightSky();
        RenderSettings.sun = sunObject != null ? sunObject.GetComponent<Light>() : null;
        DynamicGI.UpdateEnvironment();

        // 손전등: 카메라를 따라 정면을 비춘다. 조금 옆에서 비춰 그림자가 생기게 한다.
        Transform flashlight = mainCamera.transform.Find("Flashlight");
        if (flashlight == null)
        {
            var flashlightObject = new GameObject("Flashlight");
            Undo.RegisterCreatedObjectUndo(flashlightObject, UndoName);
            flashlight = flashlightObject.transform;
            flashlight.SetParent(mainCamera.transform, false);
        }

        flashlight.localPosition = new Vector3(0.35f, -0.25f, 0.2f);
        flashlight.localRotation = Quaternion.Euler(1.5f, -1f, 0f);
        Light spot = flashlight.GetComponent<Light>();
        if (spot == null)
        {
            spot = Undo.AddComponent<Light>(flashlight.gameObject);
        }

        spot.type = LightType.Spot;
        spot.color = new Color(1f, 0.93f, 0.8f);
        spot.intensity = 90f;
        spot.range = 45f;
        spot.spotAngle = 70f;
        spot.innerSpotAngle = 35f;
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.95f;
        spot.lightmapBakeType = LightmapBakeType.Realtime;

        Undo.RecordObject(mainCamera, UndoName);
        mainCamera.farClipPlane = 140f;

        // 손전등이 카메라(머리) 쪽에 있어서 플레이어 몸과 총이 그림자로 빛을 가린다. 플레이어는 그림자를 드리우지 않게 한다.
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                Undo.RecordObject(renderer, UndoName);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // 안개 너머는 보이지 않으므로 그림자 거리를 줄여 그림자 해상도를 가까운 곳에 쓴다.
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null)
        {
            Undo.RecordObject(pipeline, UndoName);
            pipeline.shadowDistance = 45f;
            EditorUtility.SetDirty(pipeline);
        }

        Lightmapping.lightingSettings = LoadOrCreateLightingSettings();
    }

    private static Material LoadOrCreateNightSky()
    {
        string path = $"{MaterialFolder}/{SkyMaterialName}.mat";
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (sky == null)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, path);
        }

        sky.SetFloat("_SunDisk", 0f);
        sky.SetFloat("_SunSize", 0f);
        sky.SetFloat("_AtmosphereThickness", 0.4f);
        sky.SetColor("_SkyTint", new Color(0.12f, 0.15f, 0.22f));
        sky.SetColor("_GroundColor", FogColor);
        sky.SetFloat("_Exposure", 0.06f);
        EditorUtility.SetDirty(sky);
        return sky;
    }

    // 간접광과 앰비언트 오클루전만 굽는다. (그림자와 손전등·등불은 실시간)
    private static LightingSettings LoadOrCreateLightingSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "HorrorForestLighting" };
            AssetDatabase.CreateAsset(settings, LightingSettingsPath);
        }

        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        settings.lightmapResolution = 3f;
        settings.lightmapPadding = 2;
        settings.lightmapMaxSize = 2048;
        settings.directionalityMode = LightmapsMode.NonDirectional;
        settings.ao = true;
        settings.aoMaxDistance = 2.5f;
        settings.aoExponentIndirect = 1.5f;
        settings.aoExponentDirect = 0.5f;
        settings.maxBounces = 2;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 256;
        settings.environmentSampleCount = 256;
        EditorUtility.SetDirty(settings);
        return settings;
    }

    // ───────────────────────── 후처리 ─────────────────────────

    // 샘플 프로필은 그대로 두고 공포 숲 전용 프로필을 만들어 Global Volume에 연결한다.
    private static void SetupPostProcessing()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }

        Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
        color.postExposure.Override(0.7f);
        color.contrast.Override(28f);
        color.saturation.Override(-48f);
        color.colorFilter.Override(new Color(0.84f, 0.95f, 0.93f));

        WhiteBalance whiteBalance = GetOrAdd<WhiteBalance>(profile);
        whiteBalance.temperature.Override(-18f);
        whiteBalance.tint.Override(6f);

        ShadowsMidtonesHighlights tones = GetOrAdd<ShadowsMidtonesHighlights>(profile);
        tones.shadows.Override(new Vector4(0.92f, 1f, 1.05f, -0.08f));
        tones.midtones.Override(new Vector4(0.97f, 1f, 1f, -0.03f));

        Vignette vignette = GetOrAdd<Vignette>(profile);
        vignette.color.Override(Color.black);
        vignette.intensity.Override(0.48f);
        vignette.smoothness.Override(0.55f);

        FilmGrain grain = GetOrAdd<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.5f);
        grain.response.Override(0.7f);

        ChromaticAberration aberration = GetOrAdd<ChromaticAberration>(profile);
        aberration.intensity.Override(0.15f);

        Bloom bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(0.9f);
        bloom.scatter.Override(0.75f);

        EditorUtility.SetDirty(profile);

        GameObject volumeObject = FindRoot("Global Volume");
        if (volumeObject != null)
        {
            Volume volume = volumeObject.GetComponent<Volume>();
            Undo.RecordObject(volume, UndoName);
            volume.sharedProfile = profile;
        }

        StrengthenSsao();
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component))
        {
            component.active = true;
            return component;
        }

        component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    // 나무 밑동과 바위 틈이 더 어둡게 가라앉도록 렌더러의 SSAO를 강하게 한다.
    private static void StrengthenSsao()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null)
        {
            return;
        }

        var pipelineSerialized = new SerializedObject(pipeline);
        SerializedProperty rendererList = pipelineSerialized.FindProperty("m_RendererDataList");
        for (int i = 0; i < rendererList.arraySize; i++)
        {
            var rendererData = rendererList.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (rendererData == null)
            {
                continue;
            }

            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion)
                {
                    var serialized = new SerializedObject(feature);
                    SetFloat(serialized, "m_Settings.Intensity", 2f);
                    SetFloat(serialized, "m_Settings.Radius", 0.45f);
                    SetFloat(serialized, "m_Settings.DirectLightingStrength", 0.6f);
                    serialized.ApplyModifiedProperties();
                }
            }
        }
    }

    private static void SetFloat(SerializedObject serialized, string path, float value)
    {
        SerializedProperty property = serialized.FindProperty(path);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    // ───────────────────────── HUD ─────────────────────────

    private static void BuildCombatHud(Canvas canvas, ZombieManager zombieManager, PlayerGunFire gun)
    {
        if (gun == null)
        {
            Debug.LogWarning("Player에 PlayerGunFire가 없어 전투 HUD를 만들지 않았습니다.");
            return;
        }

        RectTransform hud = FindOrCreateRect(canvas.transform, "CombatHud");
        Stretch(hud);

        Text zombieText = CreateText(hud, "ZombieCounter", 34, TextAnchor.UpperCenter, new Color(0.85f, 0.12f, 0.1f));
        Place(zombieText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(800f, 60f));

        // 좀비가 끝없이 나오므로 예전의 승리 화면은 지운다.
        Transform victory = hud.Find("VictoryPanel");
        if (victory != null)
        {
            Undo.DestroyObjectImmediate(victory.gameObject);
        }

        CombatHud combatHud = GetOrAddComponent<CombatHud>(hud.gameObject);
        var serialized = new SerializedObject(combatHud);
        serialized.FindProperty("_zombieManager").objectReferenceValue = zombieManager;
        serialized.FindProperty("_zombieText").objectReferenceValue = zombieText;
        serialized.ApplyModifiedProperties();
    }

    private static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment, Color color)
    {
        RectTransform rect = FindOrCreateRect(parent, name);
        Text text = GetOrAddComponent<Text>(rect.gameObject);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;

        Shadow shadow = GetOrAddComponent<Shadow>(rect.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static RectTransform FindOrCreateRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return (RectTransform)existing;
        }

        GameObject created = ObjectFactory.CreateGameObject(name, typeof(RectTransform));
        GameObjectUtility.SetParentAndAlign(created, parent.gameObject);
        return (RectTransform)created.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ───────────────────────── 공용 ─────────────────────────

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static Transform Group(Transform parent, string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
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

    private static void SetObjectArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
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
