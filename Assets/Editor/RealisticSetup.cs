using System.IO;
using UnityEditor;
using UnityEngine;

// 맵/좀비를 사실적으로 보이게 하는 일회성 셋업 도구 (Tools > Realistic Setup)
public static class RealisticSetup
{
    private const string Dir = "Assets/09.Materials/Realistic";
    private const int Size = 256;

    [MenuItem("Tools/Realistic Setup/Textures And Materials")]
    public static void BuildTexturesAndMaterials()
    {
        if (!AssetDatabase.IsValidFolder(Dir))
        {
            AssetDatabase.CreateFolder("Assets/09.Materials", "Realistic");
        }

        MakeSet("Concrete", 5f, 3.1f, new Color(0.72f, 0.72f, 0.73f), new Color(0.97f, 0.96f, 0.95f), 1.5f);
        MakeSet("ZombieSkin", 6f, 11.7f, new Color(0.40f, 0.48f, 0.34f), new Color(0.68f, 0.73f, 0.58f), 2f);
        MakeSet("ZombieCloth", 14f, 27.3f, new Color(0.26f, 0.27f, 0.30f), new Color(0.52f, 0.48f, 0.42f), 2.5f);
        AssetDatabase.Refresh();

        MakeLit("ZombieSkin", Color.white, 0.28f, "ZombieSkin", Vector2.one);
        MakeLit("ZombieCloth", Color.white, 0.12f, "ZombieCloth", Vector2.one);
        var blood = MakeLit("ZombieBlood", new Color(0.30f, 0.02f, 0.02f), 0.75f, null, Vector2.one);
        var eye = MakeLit("ZombieEye", new Color(0.9f, 0.75f, 0.2f), 0.9f, null, Vector2.one);
        eye.EnableKeyword("_EMISSION");
        eye.SetColor("_EmissionColor", new Color(1.0f, 0.55f, 0.08f) * 2.5f);
        EditorUtility.SetDirty(eye);

        // 맵 재질에 콘크리트 질감 적용
        ApplyConcrete("GroundMat", new Vector2(30f, 30f));
        ApplyConcrete("Lit", new Vector2(2f, 2f));
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Realistic Setup/Build Zombie Bodies")]
    public static void BuildZombieBodies()
    {
        var skin = Load("ZombieSkin");
        var cloth = Load("ZombieCloth");
        var blood = Load("ZombieBlood");
        var eye = Load("ZombieEye");

        foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None))
        {
            var root = zombie.transform;
            var old = root.Find("Body");
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            var rootRenderer = zombie.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
            {
                Undo.RecordObject(rootRenderer, "Hide capsule");
                rootRenderer.enabled = false;
            }

            var body = New("Body", root, Vector3.zero);

            // 다리 (엉덩이 높이를 축으로 회전)
            var leftLeg = New("LeftLeg", body, new Vector3(-0.17f, -0.05f, 0f));
            var rightLeg = New("RightLeg", body, new Vector3(0.17f, -0.05f, 0f));
            Prim(PrimitiveType.Capsule, "Leg", leftLeg, new Vector3(0f, -0.47f, 0f), new Vector3(0.22f, 0.47f, 0.24f), cloth);
            Prim(PrimitiveType.Capsule, "Leg", rightLeg, new Vector3(0f, -0.47f, 0f), new Vector3(0.22f, 0.47f, 0.24f), cloth);
            Prim(PrimitiveType.Sphere, "Foot", leftLeg, new Vector3(0f, -0.93f, 0.06f), new Vector3(0.2f, 0.12f, 0.32f), cloth);
            Prim(PrimitiveType.Sphere, "Foot", rightLeg, new Vector3(0f, -0.93f, 0.06f), new Vector3(0.2f, 0.12f, 0.32f), cloth);

            // 상체
            var upper = New("UpperBody", body, new Vector3(0f, -0.05f, 0f));
            Prim(PrimitiveType.Sphere, "Torso", upper, new Vector3(0f, 0.33f, 0f), new Vector3(0.52f, 0.78f, 0.3f), cloth);
            Prim(PrimitiveType.Sphere, "Blood", upper, new Vector3(0.1f, 0.38f, 0.13f), new Vector3(0.18f, 0.24f, 0.05f), blood);
            Prim(PrimitiveType.Sphere, "Blood", upper, new Vector3(-0.12f, 0.12f, 0.12f), new Vector3(0.14f, 0.1f, 0.05f), blood);
            Prim(PrimitiveType.Sphere, "Blood", upper, new Vector3(0.04f, 0.58f, 0.1f), new Vector3(0.22f, 0.08f, 0.06f), blood);

            // 머리
            var head = New("Head", upper, new Vector3(0f, 0.72f, 0.02f));
            Prim(PrimitiveType.Sphere, "Skull", head, new Vector3(0f, 0.15f, 0f), new Vector3(0.3f, 0.34f, 0.32f), skin);
            Prim(PrimitiveType.Sphere, "Jaw", head, new Vector3(0f, 0.02f, 0.07f), new Vector3(0.2f, 0.1f, 0.2f), skin);
            Prim(PrimitiveType.Sphere, "Eye", head, new Vector3(-0.07f, 0.18f, 0.14f), new Vector3(0.05f, 0.04f, 0.04f), eye);
            Prim(PrimitiveType.Sphere, "Eye", head, new Vector3(0.07f, 0.18f, 0.14f), new Vector3(0.05f, 0.04f, 0.04f), eye);
            Prim(PrimitiveType.Sphere, "Blood", head, new Vector3(0.03f, 0.0f, 0.15f), new Vector3(0.1f, 0.07f, 0.04f), blood);

            // 팔
            var leftArm = New("LeftArm", upper, new Vector3(-0.33f, 0.62f, 0f));
            var rightArm = New("RightArm", upper, new Vector3(0.33f, 0.62f, 0f));
            foreach (var arm in new[] { leftArm, rightArm })
            {
                Prim(PrimitiveType.Capsule, "Sleeve", arm, new Vector3(0f, -0.2f, 0f), new Vector3(0.16f, 0.2f, 0.16f), cloth);
                Prim(PrimitiveType.Capsule, "Forearm", arm, new Vector3(0f, -0.52f, 0f), new Vector3(0.12f, 0.2f, 0.12f), skin);
                Prim(PrimitiveType.Sphere, "Hand", arm, new Vector3(0f, -0.76f, 0f), new Vector3(0.12f, 0.16f, 0.07f), skin);
            }

            // 애니메이션 컴포넌트 연결
            var visual = body.gameObject.GetComponent<ZombieVisual>();
            if (visual == null)
            {
                visual = Undo.AddComponent<ZombieVisual>(body.gameObject);
            }
            var so = new SerializedObject(visual);
            so.FindProperty("_upperBody").objectReferenceValue = upper;
            so.FindProperty("_head").objectReferenceValue = head;
            so.FindProperty("_leftArm").objectReferenceValue = leftArm;
            so.FindProperty("_rightArm").objectReferenceValue = rightArm;
            so.FindProperty("_leftLeg").objectReferenceValue = leftLeg;
            so.FindProperty("_rightLeg").objectReferenceValue = rightLeg;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(zombie.gameObject);
        }
    }

    private static Transform New(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Zombie body");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    private static void Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Undo.RegisterCreatedObjectUndo(go, "Zombie body");
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    private static Material Load(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(Dir + "/" + name + ".mat");
    }

    private static Material MakeLit(string name, Color color, float smoothness, string texSet, Vector2 tiling)
    {
        string path = Dir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        if (texSet != null)
        {
            ApplyTextures(mat, texSet, tiling);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void ApplyConcrete(string materialName, Vector2 tiling)
    {
        foreach (var guid in AssetDatabase.FindAssets(materialName + " t:Material"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != materialName)
            {
                continue;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            ApplyTextures(mat, "Concrete", tiling);
            mat.SetFloat("_Smoothness", 0.22f);
            EditorUtility.SetDirty(mat);
        }
    }

    private static void ApplyTextures(Material mat, string set, Vector2 tiling)
    {
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + set + "_Albedo.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + set + "_Normal.png");
        mat.SetTexture("_BaseMap", albedo);
        mat.SetTextureScale("_BaseMap", tiling);
        mat.SetTexture("_BumpMap", normal);
        mat.SetTextureScale("_BumpMap", tiling);
        mat.SetFloat("_BumpScale", 0.5f);
        mat.EnableKeyword("_NORMALMAP");
    }

    // 타일링되는 멀티 옥타브 노이즈 텍스처와 그로부터 계산한 노멀맵을 만든다.
    private static void MakeSet(string name, float scale, float seed, Color dark, Color light, float normalStrength)
    {
        var height = new float[Size * Size];
        var albedo = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
        var px = new Color[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = (float)x / Size * Mathf.PI * 2f;
                float v = (float)y / Size * Mathf.PI * 2f;
                float n = 0f, amp = 0.5f, f = scale;
                for (int o = 0; o < 5; o++)
                {
                    // 원기둥 두 개를 감아 샘플링해서 가장자리가 이어지도록 한다.
                    float a = Mathf.PerlinNoise(seed + Mathf.Cos(u) * f * 0.3f + Mathf.Cos(v) * f * 0.3f,
                                                seed + Mathf.Sin(u) * f * 0.3f + Mathf.Sin(v) * f * 0.3f);
                    n += a * amp;
                    amp *= 0.5f;
                    f *= 2f;
                }
                n = Mathf.Clamp01(n * 1.15f - 0.05f);
                height[y * Size + x] = n;
                px[y * Size + x] = Color.Lerp(dark, light, n);
            }
        }
        albedo.SetPixels(px);
        albedo.Apply();
        Save(albedo, Dir + "/" + name + "_Albedo.png", false);

        var normal = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        var npx = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float l = height[y * Size + (x + Size - 1) % Size];
                float r = height[y * Size + (x + 1) % Size];
                float d = height[((y + Size - 1) % Size) * Size + x];
                float up = height[((y + 1) % Size) * Size + x];
                var nrm = new Vector3((l - r) * normalStrength, (d - up) * normalStrength, 1f).normalized;
                npx[y * Size + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
            }
        }
        normal.SetPixels(npx);
        normal.Apply();
        Save(normal, Dir + "/" + name + "_Normal.png", true);
    }

    private static void Save(Texture2D tex, string path, bool isNormal)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !isNormal;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.SaveAndReimport();
    }
}
