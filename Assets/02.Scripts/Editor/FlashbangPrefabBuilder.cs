using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 섬광탄 프리팹을 만들고 플레이어의 PlayerFlashbangThrow에 연결한다.
// 올리브색 금속 깡통 + 불이 붙으면 빛나는 흰 띠 + 안전 손잡이. 포인트 라이트가 주변을 밝힌다.
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다.
public static class FlashbangPrefabBuilder
{
    private const string PrefabPath = "Assets/03.Prefabs/Weapons/Flashbang.prefab";
    private const string MeshPath = "Assets/05.Models/Weapons/Flashbang.asset";
    private const string MaterialFolder = "Assets/09.Materials/Weapons";

    private const float Radius = 0.04f;
    private const float Height = 0.14f;

    [MenuItem("Tools/Weapons/Build Flashbang")]
    private static void Build()
    {
        Mesh mesh = SaveAsset(BuildMesh(), MeshPath);
        Material body = LoadOrCreateMaterial("FlashbangBodyMat", new Color(0.2f, 0.22f, 0.16f), 0.45f, 0.6f, false);
        Material glow = LoadOrCreateMaterial("FlashbangGlowMat", new Color(0.9f, 0.9f, 0.85f), 0.3f, 0f, true);

        GameObject root = new GameObject("Flashbang");
        try
        {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { body, glow };
            // 빛이 깡통 안에서 나오므로 깡통 자신은 그림자를 드리우지 않게 한다. (아니면 빛을 전부 가린다)
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = Radius;
            collider.height = Height;
            collider.center = Vector3.zero;
            collider.sharedMaterial = LoadOrCreatePhysicsMaterial();

            var body3D = root.AddComponent<Rigidbody>();
            body3D.mass = 0.4f;
            body3D.linearDamping = 0.05f;
            body3D.angularDamping = 0.5f;
            body3D.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body3D.interpolation = RigidbodyInterpolation.Interpolate;

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root.transform, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.range = 32f;
            light.intensity = 0f;
            light.shadows = LightShadows.Soft;
            light.shadowNearPlane = 0.1f;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.enabled = false;

            var projectile = root.AddComponent<FlashbangProjectile>();
            var serialized = new SerializedObject(projectile);
            serialized.FindProperty("_light").objectReferenceValue = light;
            serialized.FindProperty("_glowRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("_glowMaterialIndex").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder("Assets/03.Prefabs/Weapons");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssignToPlayer(prefab.GetComponent<FlashbangProjectile>());
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"섬광탄 프리팹을 만들었습니다: {PrefabPath}");
    }

    private static void AssignToPlayer(FlashbangProjectile prefab)
    {
        PlayerFlashbangThrow thrower = Object.FindFirstObjectByType<PlayerFlashbangThrow>();
        if (thrower == null)
        {
            Debug.LogWarning("씬에서 PlayerFlashbangThrow를 찾지 못해 프리팹을 연결하지 않았습니다.");
            return;
        }

        var serialized = new SerializedObject(thrower);
        serialized.FindProperty("_flashbangPrefab").objectReferenceValue = prefab;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(thrower.gameObject.scene);
    }

    // 서브메시 0 = 깡통·뚜껑·손잡이, 1 = 빛나는 띠. 중심이 원점이고 세로로 서 있다.
    private static Mesh BuildMesh()
    {
        // 내장 "Cylinder.fbx"는 반지름 1이라, 반지름 0.5짜리 "New-Cylinder.fbx"를 쓴다. (콜라이더 크기와 맞추기 위해)
        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        var bodyParts = new[]
        {
            Part(cylinder, new Vector3(0f, 0f, 0f), Vector3.zero, new Vector3(Radius * 2f, Height * 0.5f, Radius * 2f)),
            Part(cylinder, new Vector3(0f, Height * 0.5f + 0.008f, 0f), Vector3.zero, new Vector3(Radius * 1.5f, 0.008f, Radius * 1.5f)),
            Part(cube, new Vector3(Radius + 0.006f, Height * 0.15f, 0f), new Vector3(0f, 0f, -8f), new Vector3(0.01f, Height * 0.8f, 0.022f)),
        };

        var glowParts = new[]
        {
            Part(cylinder, new Vector3(0f, -Height * 0.22f, 0f), Vector3.zero, new Vector3(Radius * 2.08f, 0.012f, Radius * 2.08f)),
            Part(cylinder, new Vector3(0f, Height * 0.2f, 0f), Vector3.zero, new Vector3(Radius * 2.08f, 0.008f, Radius * 2.08f)),
        };

        Mesh bodyMesh = Combine(bodyParts, true);
        Mesh glowMesh = Combine(glowParts, true);
        var mesh = new Mesh { name = "Flashbang" };
        mesh.CombineMeshes(new[]
        {
            new CombineInstance { mesh = bodyMesh, transform = Matrix4x4.identity },
            new CombineInstance { mesh = glowMesh, transform = Matrix4x4.identity },
        }, false, true);
        mesh.RecalculateBounds();

        Object.DestroyImmediate(bodyMesh);
        Object.DestroyImmediate(glowMesh);
        return mesh;
    }

    private static CombineInstance Part(Mesh mesh, Vector3 position, Vector3 euler, Vector3 scale)
    {
        return new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.Euler(euler), scale) };
    }

    private static Mesh Combine(CombineInstance[] parts, bool merge)
    {
        var mesh = new Mesh();
        mesh.CombineMeshes(parts, merge, true);
        return mesh;
    }

    private static Material LoadOrCreateMaterial(string materialName, Color color, float smoothness, float metallic, bool emissive)
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
        material.SetFloat("_Metallic", metallic);

        if (emissive)
        {
            // 평소에는 빛나지 않고, 불이 붙으면 FlashbangProjectile이 발광 색을 올린다.
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static PhysicsMaterial LoadOrCreatePhysicsMaterial()
    {
        const string path = "Assets/09.Materials/Weapons/FlashbangPhysics.physicMaterial";
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (material == null)
        {
            material = new PhysicsMaterial("FlashbangPhysics");
            AssetDatabase.CreateAsset(material, path);
        }

        material.bounciness = 0.25f;
        material.dynamicFriction = 0.6f;
        material.staticFriction = 0.7f;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh SaveAsset(Mesh mesh, string path)
    {
        EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
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
