using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 맵 바깥 아득히 먼 곳에, 지평선을 덮을 만큼 거대한 눈 덩어리 하나를 세운다. (움직이지 않고 체력이 무한한 적)
//  - 크기: 폭 약 1.4km, 높이 약 0.8km. 탑 꼭대기에서 바라보면 정면 화면을 거의 다 덮는다.
//  - 모양: 겹쳐 쌓인 살덩이 더미 + 하늘로 꿈틀대는 거대한 촉수 + 몸 곳곳에 박힌 수많은 눈알.
//    눈은 두 가지다: EYE ADVANCED(고양이·짐승 눈)와 NEFASA Eyes(사람 눈. 몸 밖으로 불거지고 시신경이 몸으로 이어진다).
//  - 바라보면 화면이 글리치하도록 ColossusGlitch를 붙인다. (렌더러 등록은 Tools > Monsters > Install Colossus Glitch)
//  - 너무 멀어서 안개에 묻히지 않도록 이 존재의 재질은 안개를 적게 받고, 카메라 먼 평면을 늘리며, 지평선 쪽으로 땅을 넓게 깐다.
// 이름으로 찾아 다시 만들므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다. (Ctrl+Z로 되돌릴 수 있다)
public static class CosmicColossusBuilder
{
    private const string UndoName = "Build Cosmic Colossus";
    private const string RootName = "CosmicColossus";
    private const string FarGroundName = "FarGround";
    private const string MaterialFolder = "Assets/09.Materials/Monsters/";
    private const string GroundMaterialPath = "Assets/09.Materials/Forest/ForestGroundMat.mat";

    private const float Distance = 720f;          // 플레이어(원점)에서 몸 중심까지의 거리 (+Z 방향, 시작할 때 보는 쪽)
    private const float EyeDiameter = 0.04f;      // EYE ADVANCED 눈 메시의 지름(미터)
    private const float CameraFarClip = 4000f;
    private const float ColossusFogAmount = 0.25f; // 1이면 다른 물체처럼 안개를 다 받는다.
    private const int EyeCount = 120;            // 모든 눈의 총 개수(거대한 눈과 NEFASA 눈 포함)
    private const int GiantEyeCount = 6;
    private const int NefasaEyeCount = 34;
    private const float NefasaEyeDiameter = 0.024f; // NEFASA 눈알의 지름(미터)
    private const string NefasaPrefabPath = "Assets/NEFASA_Eyes/Pipelines/URP/Prefabs/SingleEye.prefab";
    private const string NefasaMaterialFolder = "Assets/NEFASA_Eyes/Pipelines/URP/Materials/";
    private const int TentacleCount = 9;
    private const int FarGroundLayer = 5;         // 미니맵 카메라가 그리지 않는 층

    private struct Lump
    {
        public Vector3 Center;
        public float Radius;
    }

    [MenuItem("Tools/Monsters/Build Cosmic Colossus")]
    private static void Build()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera를 찾지 못했습니다.");
            return;
        }

        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();

        RemoveOld(RootName);
        RemoveOld(FarGroundName);

        Material skin = CreateCopy("ColossusSkinMat", MaterialFolder + "CosmicZombieMat.mat");
        Material tentacle = CreateCopy("ColossusTentacleMat", MaterialFolder + "GrotesqueTentacleMat.mat");
        var eyeMaterials = new[]
        {
            CreateCopy("ColossusEye_Amber", MaterialFolder + "GrotesqueEye_Amber.mat"),
            CreateCopy("ColossusEye_Green", MaterialFolder + "GrotesqueEye_Green.mat"),
            CreateCopy("ColossusEye_Red", MaterialFolder + "GrotesqueEye_Red.mat"),
            CreateCopy("ColossusEye_Cat", MaterialFolder + "GrotesqueEye_Cat.mat"),
        };
        Mesh sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        Mesh eyeMesh = LoadEyeMesh();
        var nefasaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NefasaPrefabPath);
        Dictionary<string, Material> nefasaMaterials = CreateNefasaMaterials();
        if (nefasaPrefab == null || nefasaMaterials == null)
        {
            Debug.LogError("NEFASA Eyes 프리팹 또는 재질/셰이더를 찾지 못했습니다.");
            return;
        }

        if (skin == null || tentacle == null || eyeMesh == null || System.Array.IndexOf(eyeMaterials, null) >= 0)
        {
            Debug.LogError("거대 존재에 쓸 재질이나 눈 메시를 찾지 못했습니다. Tools > Monsters > Build Grotesque Zombies를 먼저 실행하세요.");
            return;
        }

        // 멀리서도 살덩이로 읽히도록 좀비보다 밝고 덜 찢어진 살갗, 뚜렷한 가장자리 빛
        skin.SetFloat("_VoidAmount", 0.12f);
        skin.SetColor("_BaseColor", new Color(2.2f, 2f, 2f));
        skin.SetColor("_RimColor", new Color(0.7f, 0.8f, 0.35f));
        skin.SetFloat("_RimPower", 1.8f);
        skin.SetFloat("_FogAmount", 0.08f);
        skin.SetFloat("_Glow", 0.3f);     // 달빛을 못 받는 쪽 면도 창백한 살덩이로 보이게
        EditorUtility.SetDirty(skin);

        var random = new System.Random(1313);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, UndoName);
        root.transform.position = new Vector3(0f, 0f, Distance);

        // 몸: 숨 쉴 때 이 오브젝트만 부풀므로 눈·촉수는 모두 이 아래에 둔다.
        var body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);

        List<Lump> lumps = BuildLumps(random);
        foreach (Lump lump in lumps)
        {
            CreateLump(body.transform, lump, sphere, skin);
        }

        var eyes = new List<Transform>();
        PlaceEyes(body.transform, lumps, eyeMesh, eyeMaterials, nefasaPrefab, nefasaMaterials, random, eyes);
        BuildTentacles(body.transform, lumps, tentacle, random);

        var colossus = root.AddComponent<CosmicColossus>();
        var serialized = new SerializedObject(colossus);
        serialized.FindProperty("_target").objectReferenceValue = mainCamera.transform;
        serialized.FindProperty("_body").objectReferenceValue = body.transform;
        SerializedProperty eyeArray = serialized.FindProperty("_eyes");
        eyeArray.arraySize = eyes.Count;
        for (int i = 0; i < eyes.Count; i++)
        {
            eyeArray.GetArrayElementAtIndex(i).objectReferenceValue = eyes[i];
        }

        serialized.ApplyModifiedProperties();

        var glitch = root.AddComponent<ColossusGlitch>();
        var glitchSerialized = new SerializedObject(glitch);
        glitchSerialized.FindProperty("_viewer").objectReferenceValue = mainCamera.transform;
        glitchSerialized.ApplyModifiedProperties();

        var madness = root.AddComponent<MadnessEffect>();
        var madnessSerialized = new SerializedObject(madness);
        madnessSerialized.FindProperty("_source").objectReferenceValue = glitch;
        madnessSerialized.FindProperty("_camera").objectReferenceValue = mainCamera.transform;
        madnessSerialized.ApplyModifiedProperties();

        BuildFarGround();

        Undo.RecordObject(mainCamera, UndoName);
        mainCamera.farClipPlane = CameraFarClip;

        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"거대 존재를 만들었습니다. 살덩이 {lumps.Count}개, 눈 {eyes.Count}개, 촉수 {TentacleCount}개. 결과를 확인한 뒤 씬을 저장하세요.");
    }

    // ───────────────────────── 몸 ─────────────────────────

    // 아래에서 위로 갈수록 좁아지는 살덩이 더미. 로컬 좌표(몸 중심 기준, 플레이어 쪽이 -Z).
    private static List<Lump> BuildLumps(System.Random random)
    {
        var lumps = new List<Lump>();

        for (int i = 0; i < 9; i++)
        {
            float t = i / 8f;
            lumps.Add(new Lump
            {
                Center = new Vector3(Mathf.Lerp(-640f, 640f, t), Range(random, 50f, 130f), Range(random, -90f, 90f)),
                Radius = Range(random, 210f, 320f),
            });
        }

        for (int i = 0; i < 7; i++)
        {
            float t = i / 6f;
            lumps.Add(new Lump
            {
                Center = new Vector3(Mathf.Lerp(-420f, 420f, t), Range(random, 250f, 350f), Range(random, -120f, 0f)),
                Radius = Range(random, 170f, 260f),
            });
        }

        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            lumps.Add(new Lump
            {
                Center = new Vector3(Mathf.Lerp(-240f, 240f, t), Range(random, 460f, 580f), Range(random, -70f, 30f)),
                Radius = Range(random, 120f, 190f),
            });
        }

        lumps.Add(new Lump { Center = new Vector3(-90f, 690f, 0f), Radius = 110f });
        lumps.Add(new Lump { Center = new Vector3(110f, 710f, -10f), Radius = 95f });

        // 앞쪽(플레이어 쪽)으로 불거진 혹
        for (int i = 0; i < 6; i++)
        {
            lumps.Add(new Lump
            {
                Center = new Vector3(Range(random, -480f, 480f), Range(random, 160f, 480f), Range(random, -230f, -130f)),
                Radius = Range(random, 70f, 120f),
            });
        }

        return lumps;
    }

    private static void CreateLump(Transform parent, Lump lump, Mesh sphere, Material skin)
    {
        var part = new GameObject("Lump");
        part.transform.SetParent(parent, false);
        part.transform.localPosition = lump.Center;
        part.transform.localScale = Vector3.one * lump.Radius * 2f;
        part.AddComponent<MeshFilter>().sharedMesh = sphere;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = skin;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        part.AddComponent<SphereCollider>().radius = 0.5f; // 총알이 맞는 곳
    }

    // ───────────────────────── 눈 ─────────────────────────

    // 플레이어 쪽을 향한 표면에 눈을 박는다. 먼저 몇 개의 거대한 눈을, 그다음 크고 작은 눈들을 겹치지 않게 채운다.
    private static void PlaceEyes(Transform body, List<Lump> lumps, Mesh eyeMesh, Material[] materials,
        GameObject nefasaPrefab, Dictionary<string, Material> nefasaMaterials, System.Random random, List<Transform> eyes)
    {
        var placed = new List<(Vector3 position, float size)>();

        // 반지름에 비례해 눈이 많이 박히도록 제곱으로 가중
        var weights = new float[lumps.Count];
        float totalWeight = 0f;
        for (int i = 0; i < lumps.Count; i++)
        {
            totalWeight += lumps[i].Radius * lumps[i].Radius;
            weights[i] = totalWeight;
        }

        // 단계: 거대한 눈 → 사람 눈 → 나머지. 한 단계가 자리를 못 찾아도 시도 횟수가 차면 다음 단계로 넘어간다.
        int giantPlaced = 0;
        int humanPlaced = 0;
        for (int attempt = 0; attempt < 14000 && placed.Count < EyeCount; attempt++)
        {
            bool giant = giantPlaced < GiantEyeCount && attempt < 1500;
            bool human = !giant && humanPlaced < NefasaEyeCount && attempt < 6000;
            int lumpIndex = PickWeighted(weights, (float)random.NextDouble() * totalWeight);
            Lump lump = lumps[lumpIndex];
            if (giant && (lump.Radius < 170f || lump.Center.y < 200f))
            {
                continue; // 거대한 눈은 중단 이상의 큰 살덩이에만
            }

            Vector3 direction = RandomDirection(random);
            if (direction.z > 0.05f || direction.y < -0.15f)
            {
                continue; // 뒤쪽·아래쪽(땅속)은 보이지 않는다.
            }

            float size = giant ? Range(random, 200f, 320f) : human ? Range(random, 150f, 340f) : Range(random, 0.14f, 0.38f) * lump.Radius;
            if (human && lump.Radius < 150f)
            {
                continue; // 사람 눈은 큰 살덩이에만
            }

            Vector3 surface = lump.Center + direction * lump.Radius;
            if (IsInsideOtherLump(lumps, lumpIndex, surface))
            {
                continue;
            }

            // 사람 눈은 몸 밖으로 불거져 시신경이 살덩이로 이어진다.
            Vector3 position = surface + direction * size * (human ? 0.55f : 0.15f);
            if (IsCrowded(placed, position, size))
            {
                continue;
            }

            placed.Add((position, size));
            giantPlaced += giant ? 1 : 0;
            humanPlaced += human ? 1 : 0;
            if (human)
            {
                eyes.Add(CreateNefasaEye(body, nefasaPrefab, nefasaMaterials, position, direction, size, random));
                continue;
            }

            var eye = new GameObject("Eye");
            eye.transform.SetParent(body, false);
            eye.transform.localPosition = position;
            eye.transform.localRotation = Quaternion.LookRotation(direction);
            eye.transform.localScale = Vector3.one * (size / EyeDiameter);
            eye.AddComponent<MeshFilter>().sharedMesh = eyeMesh;
            var renderer = eye.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[random.Next(materials.Length)];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            eyes.Add(eye.transform);
        }
    }

    // NEFASA 사람 눈 하나. 프리팹을 풀어 재질을 안개를 덜 받는 사본으로 바꾸고, 눈마다 홍채색·동공·핏줄을 기괴하게 바꾼다.
    private static Transform CreateNefasaEye(Transform body, GameObject prefab, Dictionary<string, Material> materials,
        Vector3 position, Vector3 direction, float size, System.Random random)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = "HumanEye";
        instance.transform.SetParent(body, false);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.LookRotation(direction);
        instance.transform.localScale = Vector3.one * (size / NefasaEyeDiameter);

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material[] shared = renderer.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] != null && materials.TryGetValue(shared[i].name, out Material replacement))
                {
                    shared[i] = replacement;
                }
            }

            renderer.sharedMaterials = shared;
        }

        var controller = instance.GetComponent<NEFASA.Eyes.EyeController>();
        if (controller != null)
        {
            Color[] palette =
            {
                new Color(0.75f, 0.62f, 0.08f), new Color(0.65f, 0.04f, 0.04f), new Color(0.8f, 0.8f, 0.7f),
                new Color(0.42f, 0.1f, 0.5f), new Color(0.1f, 0.55f, 0.25f),
            };
            Color iris = palette[random.Next(palette.Length)];
            controller.irisColor = iris;
            controller.innerColor = iris * 0.45f;
            controller.pupilRadius = random.NextDouble() < 0.5 ? 0.9f : (random.NextDouble() < 0.5 ? 3.2f : Range(random, 1.5f, 2.6f));
            controller.vessels = 1f;
            controller.fiberDepth = 0.6f;
            controller.moisture = 1f;
            controller.showOpticNerve = true;
            controller.ApplySettings();
        }

        return instance.transform;
    }

    // NEFASA 재질을 복사해, 안개를 덜 받는 사본 셰이더(Assets/10.Shaders/NefasaEyes)로 바꾼다. 원본 에셋은 건드리지 않는다.
    private static Dictionary<string, Material> CreateNefasaMaterials()
    {
        Shader surfaceShader = Shader.Find("Custom/NefasaEyeSurface");
        Shader corneaShader = Shader.Find("Custom/NefasaEyeCornea");
        if (surfaceShader == null || corneaShader == null)
        {
            return null;
        }

        var result = new Dictionary<string, Material>();
        foreach ((string name, Shader shader) in new[] { ("Sclera", surfaceShader), ("Iris", surfaceShader), ("Pupil", surfaceShader), ("Cornea", corneaShader) })
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(NefasaMaterialFolder + name + ".mat");
            if (source == null)
            {
                return null;
            }

            string path = MaterialFolder + "ColossusNefasa_" + name + ".mat";
            Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                copy = new Material(shader) { name = "ColossusNefasa_" + name };
                AssetDatabase.CreateAsset(copy, path);
            }

            copy.shader = shader;
            Shader sourceShader = source.shader;
            for (int i = 0; i < sourceShader.GetPropertyCount(); i++)
            {
                string property = sourceShader.GetPropertyName(i);
                if (!copy.HasProperty(property))
                {
                    continue;
                }

                switch (sourceShader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color: copy.SetColor(property, source.GetColor(property)); break;
                    case ShaderPropertyType.Vector: copy.SetVector(property, source.GetVector(property)); break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: copy.SetFloat(property, source.GetFloat(property)); break;
                    case ShaderPropertyType.Texture: copy.SetTexture(property, source.GetTexture(property)); break;
                }
            }

            copy.SetFloat("_FogAmount", ColossusFogAmount);
            copy.SetFloat("_SelfLit", 0.45f);   // 달빛을 못 받는 쪽을 봐도 눈이 보이게
            copy.enableInstancing = true;
            EditorUtility.SetDirty(copy);
            result[name] = copy;
        }

        return result;
    }

    private static bool IsInsideOtherLump(List<Lump> lumps, int self, Vector3 point)
    {
        for (int i = 0; i < lumps.Count; i++)
        {
            if (i != self && Vector3.Distance(point, lumps[i].Center) < lumps[i].Radius * 0.96f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCrowded(List<(Vector3 position, float size)> placed, Vector3 position, float size)
    {
        foreach (var other in placed)
        {
            float minDistance = (other.size + size) * 0.6f;
            if ((other.position - position).sqrMagnitude < minDistance * minDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static int PickWeighted(float[] cumulative, float value)
    {
        for (int i = 0; i < cumulative.Length; i++)
        {
            if (value <= cumulative[i])
            {
                return i;
            }
        }

        return cumulative.Length - 1;
    }

    // ───────────────────────── 촉수 ─────────────────────────

    // 위쪽·양옆 살덩이에서 하늘을 향해 천천히 꿈틀대며 뻗는 거대한 촉수
    private static void BuildTentacles(Transform body, List<Lump> lumps, Material material, System.Random random)
    {
        for (int i = 0; i < TentacleCount; i++)
        {
            Lump lump = lumps[random.Next(lumps.Count)];
            if (lump.Center.y < 200f)
            {
                i--;
                continue;
            }

            float side = Mathf.Sign(lump.Center.x == 0f ? 1f : lump.Center.x);
            Vector3 direction = new Vector3(side * Range(random, 0.2f, 0.7f), Range(random, 0.7f, 1f), Range(random, -0.2f, 0.3f)).normalized;

            var tentacleObject = new GameObject("Tentacle");
            tentacleObject.transform.SetParent(body, false);
            tentacleObject.transform.localPosition = lump.Center + direction * lump.Radius * 0.85f;
            tentacleObject.transform.localRotation = Quaternion.LookRotation(direction);

            var tentacle = tentacleObject.AddComponent<Tentacle>();
            tentacleObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            tentacleObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var serialized = new SerializedObject(tentacle);
            serialized.FindProperty("_segments").intValue = 20;
            serialized.FindProperty("_sides").intValue = 10;
            serialized.FindProperty("_length").floatValue = Range(random, 500f, 950f);
            serialized.FindProperty("_baseRadius").floatValue = Range(random, 28f, 48f);
            serialized.FindProperty("_tipRadius").floatValue = 4f;
            serialized.FindProperty("_bendAngle").floatValue = Range(random, 5f, 9f);
            serialized.FindProperty("_waveSpeed").floatValue = Range(random, 0.3f, 0.6f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ───────────────────────── 먼 땅 ─────────────────────────

    // 원래 땅(160m)이 끝나는 곳부터 거대 존재의 발밑까지 이어지는 넓고 어두운 땅. 안개에 녹아 지평선처럼 보인다.
    private static void BuildFarGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = FarGroundName;
        Undo.RegisterCreatedObjectUndo(ground, UndoName);
        ground.layer = FarGroundLayer;
        ground.transform.position = new Vector3(0f, -0.4f, 0f);
        ground.transform.localScale = new Vector3(600f, 1f, 600f); // Plane은 10m 단위 → 6km

        Object.DestroyImmediate(ground.GetComponent<MeshCollider>());
        var renderer = ground.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
    }

    // ───────────────────────── 도우미 ─────────────────────────

    // 좀비용 재질을 그대로 복사해 안개를 덜 받게 한 거대 존재 전용 재질을 만든다. (좀비 재질은 건드리지 않는다)
    private static Material CreateCopy(string name, string sourcePath)
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
        if (source == null)
        {
            return null;
        }

        string path = MaterialFolder + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(source) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.CopyPropertiesFromMaterial(source);
        }

        if (material.HasProperty("_FogAmount"))
        {
            material.SetFloat("_FogAmount", ColossusFogAmount);
        }

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh LoadEyeMesh()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/EYE ADVANCED/Mesh/EyeAdvancedObjects.fbx"))
        {
            if (asset is Mesh mesh && mesh.name == "EyeAdvanced_LOD0")
            {
                return mesh;
            }
        }

        return null;
    }

    private static void RemoveOld(string name)
    {
        GameObject old = GameObject.Find(name);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }
    }

    private static Vector3 RandomDirection(System.Random random)
    {
        float z = Range(random, -1f, 1f);
        float angle = Range(random, 0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(1f - z * z);
        return new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), z);
    }

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
}
