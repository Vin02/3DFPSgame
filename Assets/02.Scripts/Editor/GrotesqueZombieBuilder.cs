using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// 좀비의 걷기·공격 애니메이션 뼈대는 그대로 두고, 겉모습을 기괴한 코스믹 호러 존재로 다시 만든다.
//  - 얼굴 없음: 원래 머리는 GrotesqueBody가 접어 없애고, 길게 뽑힌 목 끝에 눈이 잔뜩 돋은 살덩이를 단다.
//  - 살덩이 정면은 세로로 갈라지는 아가리(안쪽에 이빨)다.
//  - 실제 살갗 위 곳곳(몸통·어깨·팔)에 눈을 박는다. 모든 눈은 플레이어를 쳐다본다.
//  - 손끝에 긴 갈고리 발톱, 등에서 꿈틀거리는 촉수.
//  - 피부는 핏기 없이 창백하고 군데군데 검붉은 공허로 찢겨 있다. (CosmicHorror 셰이더)
// 생성물 이름은 모두 "Grotesque_"로 시작하며, 다시 실행하면 지우고 새로 만든다. 같은 좀비에는 같은 모양이 나온다.
public static class GrotesqueZombieBuilder
{
    private const string Prefix = "Grotesque_";
    private const string MaterialFolder = "Assets/09.Materials/Monsters";
    private const string SkinMaterialPath = "Assets/09.Materials/Monsters/CosmicZombieMat.mat";
    private const string ConeMeshPath = "Assets/05.Models/Monsters/GrotesqueCone.asset";
    private const string SkinShaderName = "Custom/CosmicHorror";

    private static readonly string[] ZombiePrefabPaths =
    {
        "Assets/03.Prefabs/Monsters/Zombie_Enemy_1.prefab",
        "Assets/03.Prefabs/Monsters/Zombie_Enemy_2.prefab",
        "Assets/03.Prefabs/Monsters/Zombie_Enemy_3.prefab",
    };

    // 크기는 모두 "좀비 단위"(좀비 배율을 곱하기 전, 키 약 2)로 적는다.
    private const float NeckLength = 0.38f;
    private const int HeadEyeCount = 13;
    private const int BodyEyeCount = 16;
    private const int TentacleCount = 5;

    private struct Materials
    {
        public Material Skin;
        public Material Sclera;
        public Material Pupil;
        public Material MawInside;
        public Material Bone;
        public Material Tentacle;
    }

    [MenuItem("Tools/Monsters/Build Grotesque Zombies")]
    private static void Build()
    {
        foreach (string path in ZombiePrefabPaths)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            ApplyTo(prefab, false);
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        foreach (Zombie zombie in Object.FindObjectsByType<Zombie>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            ApplyTo(zombie.gameObject, true);
        }

        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        Debug.Log("기괴한 좀비 외형을 만들었습니다.");
    }

    public static void ApplyTo(GameObject zombie, bool recordUndo)
    {
        var animator = zombie.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning($"{zombie.name}: Humanoid Animator가 없어 기괴한 외형을 만들지 않았습니다.");
            return;
        }

        Materials materials = PrepareMaterials();
        // 주의: 내장 "Sphere.fbx"/"Cylinder.fbx"는 반지름 1이다. 기본 도형과 같은 반지름 0.5짜리는 "New-" 쪽이다.
        Mesh sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        Mesh cylinder = Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
        Mesh cone = LoadOrCreateCone();

        RemoveGenerated(zombie.transform, recordUndo);
        ApplySkin(zombie, materials.Skin, recordUndo);

        // 같은 좀비는 매번 같은 모양이 나오도록 이름으로 시드를 정한다.
        var random = new System.Random(zombie.name.GetHashCode());
        float unit = zombie.transform.lossyScale.x;
        var eyes = new List<Transform>();

        Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
        Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform head = BuildHead(zombie.transform, neck, headBone, unit, materials, sphere, cylinder, cone, random, eyes,
            out Transform mawLeft, out Transform mawRight, recordUndo);

        BuildBodyEyes(zombie, animator, unit, materials, sphere, random, eyes, recordUndo);
        BuildClaws(zombie.transform, animator, unit, materials.Bone, cone, random, recordUndo);
        BuildTentacles(zombie.transform, animator, unit, materials.Tentacle, random, recordUndo);

        GrotesqueBody body = zombie.GetComponent<GrotesqueBody>();
        if (body == null)
        {
            body = recordUndo ? Undo.AddComponent<GrotesqueBody>(zombie) : zombie.AddComponent<GrotesqueBody>();
        }

        var serialized = new SerializedObject(body);
        serialized.FindProperty("_animator").objectReferenceValue = animator;
        serialized.FindProperty("_head").objectReferenceValue = head;
        serialized.FindProperty("_mawLeft").objectReferenceValue = mawLeft;
        serialized.FindProperty("_mawRight").objectReferenceValue = mawRight;
        SerializedProperty eyeArray = serialized.FindProperty("_eyes");
        eyeArray.arraySize = eyes.Count;
        for (int i = 0; i < eyes.Count; i++)
        {
            eyeArray.GetArrayElementAtIndex(i).objectReferenceValue = eyes[i];
        }

        if (recordUndo)
        {
            serialized.ApplyModifiedProperties();
        }
        else
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ───────────────────────── 머리 ─────────────────────────

    // 목 뼈에서 길게 뻗은 줄기 끝에 살덩이 머리를 단다. 원래 머리는 런타임에 접혀 사라진다.
    private static Transform BuildHead(Transform zombie, Transform neck, Transform headBone, float unit, Materials materials,
        Mesh sphere, Mesh cylinder, Mesh cone, System.Random random, List<Transform> eyes,
        out Transform mawLeft, out Transform mawRight, bool recordUndo)
    {
        Vector3 up = (headBone.position - neck.position).normalized;
        if (up.sqrMagnitude < 0.01f)
        {
            up = zombie.up;
        }

        Vector3 headCenter = neck.position + up * NeckLength * unit;
        Vector3 forward = Vector3.ProjectOnPlane(zombie.forward, up).normalized;

        // 목 줄기
        Vector3 middle = (neck.position + headCenter) * 0.5f;
        float length = Vector3.Distance(neck.position, headCenter);
        GameObject stalk = CreatePart(neck, "Neck", cylinder, materials.Skin, middle, Quaternion.FromToRotation(Vector3.up, up),
            new Vector3(0.075f * unit, length * 0.5f, 0.075f * unit), recordUndo);
        stalk.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

        // 머리 기준점: 이 아래는 모두 좀비 단위로 놓는다.
        var headObject = new GameObject(Prefix + "Head");
        RegisterCreated(headObject, recordUndo);
        Transform head = headObject.transform;
        head.SetParent(neck, false);
        head.SetPositionAndRotation(headCenter, Quaternion.LookRotation(forward, up));
        SetWorldScale(head, Vector3.one * unit);

        // 머리는 몸 콜라이더(캐릭터 컨트롤러)보다 위에 있어서 총알 판정용 트리거를 따로 둔다. (트리거라 이동을 막지 않는다)
        var hitbox = headObject.AddComponent<SphereCollider>();
        hitbox.isTrigger = true;
        hitbox.radius = 0.17f;

        // 울퉁불퉁한 살덩이
        CreateLocal(head, "Lump", sphere, materials.Skin, Vector3.zero, Quaternion.identity, new Vector3(0.26f, 0.3f, 0.24f));
        CreateLocal(head, "Lump", sphere, materials.Skin, new Vector3(0.08f, 0.11f, -0.05f), Quaternion.Euler(20f, 10f, 0f), Vector3.one * 0.17f);
        CreateLocal(head, "Lump", sphere, materials.Skin, new Vector3(-0.1f, 0.07f, -0.02f), Quaternion.identity, new Vector3(0.16f, 0.14f, 0.16f));
        CreateLocal(head, "Lump", sphere, materials.Skin, new Vector3(0.05f, -0.09f, -0.08f), Quaternion.identity, Vector3.one * 0.15f);
        CreateLocal(head, "Lump", sphere, materials.Skin, new Vector3(-0.04f, 0.16f, -0.03f), Quaternion.Euler(0f, 0f, 30f), new Vector3(0.12f, 0.1f, 0.12f));

        // 세로로 갈라진 아가리: 검붉은 속과, 이빨이 늘어선 좌우 덮개
        CreateLocal(head, "MawInside", sphere, materials.MawInside, new Vector3(0f, -0.02f, 0.085f), Quaternion.identity, new Vector3(0.07f, 0.21f, 0.07f));
        mawLeft = BuildMawFlap(head, -1f, sphere, cone, materials);
        mawRight = BuildMawFlap(head, 1f, sphere, cone, materials);

        // 얼굴 자리를 눈이 뒤덮는다: 대부분 앞쪽(아가리 둘레)에, 몇 개는 옆·위에. 아가리 자리와 뒤통수는 피한다.
        int placed = 0;
        for (int attempt = 0; attempt < 600 && placed < HeadEyeCount; attempt++)
        {
            Vector3 direction = RandomDirection(random);
            bool front = direction.z > 0.25f;
            bool mawArea = Mathf.Abs(direction.x) < 0.22f && direction.z > 0.6f && Mathf.Abs(direction.y) < 0.6f;
            if (direction.z < -0.2f || mawArea || (!front && random.NextDouble() > 0.25))
            {
                continue;
            }

            Vector3 surface = Vector3.Scale(direction, new Vector3(0.13f, 0.15f, 0.12f));
            float size = Range(random, 0.04f, 0.075f);
            eyes.Add(CreateEyeLocal(head, surface, Quaternion.LookRotation(direction), size, sphere, materials));
            placed++;
        }

        return head;
    }

    // side: -1 = 왼쪽, 1 = 오른쪽. 덮개는 가운데 세로선(경첩)을 축으로 바깥으로 벌어진다.
    private static Transform BuildMawFlap(Transform head, float side, Mesh sphere, Mesh cone, Materials materials)
    {
        var pivotObject = new GameObject(Prefix + (side < 0f ? "MawLeft" : "MawRight"));
        Transform pivot = pivotObject.transform;
        pivot.SetParent(head, false);
        pivot.localPosition = new Vector3(side * 0.004f, -0.02f, 0.1f);

        CreateLocal(pivot, "MawFlap", sphere, materials.Skin, new Vector3(side * 0.045f, 0f, -0.012f), Quaternion.identity, new Vector3(0.09f, 0.23f, 0.08f));

        // 안쪽 가장자리에 가운데를 향한 이빨
        for (int i = 0; i < 7; i++)
        {
            float y = Mathf.Lerp(-0.085f, 0.085f, i / 6f);
            float toothLength = 0.03f + 0.012f * Mathf.Sin(i * 1.7f);
            CreateLocal(pivot, "Tooth", cone, materials.Bone, new Vector3(side * 0.006f, y, 0.012f),
                Quaternion.Euler(0f, 0f, side * 90f), new Vector3(0.013f, toothLength, 0.013f));
        }

        return pivot;
    }

    // ───────────────────────── 몸의 눈 ─────────────────────────

    // 실제 살갗 정점 위(몸통·어깨·팔)에 눈을 박는다. 정점의 주 뼈에 붙여서 애니메이션을 따라간다.
    private static void BuildBodyEyes(GameObject zombie, Animator animator, float unit, Materials materials, Mesh sphere,
        System.Random random, List<Transform> eyes, bool recordUndo)
    {
        var allowedBones = new HashSet<Transform>();
        foreach (HumanBodyBones bone in new[]
                 {
                     HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck,
                     HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder, HumanBodyBones.LeftUpperArm,
                     HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
                 })
        {
            Transform transform = animator.GetBoneTransform(bone);
            if (transform != null)
            {
                allowedBones.Add(transform);
            }
        }

        // 후보: 허용된 뼈에 붙은 정점들
        var candidates = new List<(Transform bone, Vector3 local, Vector3 normal, Vector3 world)>();
        foreach (SkinnedMeshRenderer renderer in zombie.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bindposes = mesh.bindposes;
            Transform[] bones = renderer.bones;
            if (weights.Length != vertices.Length || normals.Length != vertices.Length)
            {
                continue;
            }

            for (int i = 0; i < vertices.Length; i += 3)
            {
                int boneIndex = weights[i].boneIndex0;
                if (weights[i].weight0 < 0.7f || boneIndex >= bones.Length || !allowedBones.Contains(bones[boneIndex]))
                {
                    continue;
                }

                Transform bone = bones[boneIndex];
                Vector3 local = bindposes[boneIndex].MultiplyPoint3x4(vertices[i]);
                Vector3 normal = bindposes[boneIndex].MultiplyVector(normals[i]).normalized;
                candidates.Add((bone, local, normal, bone.TransformPoint(local)));
            }
        }

        var placed = new List<Vector3>();
        float minDistance = 0.11f * unit;
        for (int attempt = 0; attempt < 600 && placed.Count < BodyEyeCount && candidates.Count > 0; attempt++)
        {
            var candidate = candidates[random.Next(candidates.Count)];
            bool crowded = false;
            foreach (Vector3 other in placed)
            {
                if ((other - candidate.world).sqrMagnitude < minDistance * minDistance)
                {
                    crowded = true;
                    break;
                }
            }

            if (crowded)
            {
                continue;
            }

            float size = Range(random, 0.035f, 0.06f) * unit;
            Vector3 worldNormal = candidate.bone.TransformDirection(candidate.normal).normalized;
            Vector3 position = candidate.world + worldNormal * size * 0.2f;
            Transform eye = CreateEyeWorld(candidate.bone, position, Quaternion.LookRotation(worldNormal), size, sphere, materials, recordUndo);
            eyes.Add(eye);
            placed.Add(candidate.world);
        }
    }

    // ───────────────────────── 발톱과 촉수 ─────────────────────────

    private static void BuildClaws(Transform zombie, Animator animator, float unit, Material bone, Mesh cone, System.Random random, bool recordUndo)
    {
        foreach ((HumanBodyBones lower, HumanBodyBones hand) in new[]
                 {
                     (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                     (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
                 })
        {
            Transform lowerArm = animator.GetBoneTransform(lower);
            Transform handBone = animator.GetBoneTransform(hand);
            Vector3 direction = (handBone.position - lowerArm.position).normalized;
            Vector3 side = Vector3.Cross(direction, zombie.up).normalized;

            for (int i = 0; i < 3; i++)
            {
                float spread = (i - 1) * 0.022f * unit;
                Vector3 clawDirection = Quaternion.AngleAxis((i - 1) * 9f + Range(random, -4f, 4f), zombie.up) * direction;
                Vector3 start = handBone.position + direction * 0.05f * unit + side * spread;
                CreatePart(handBone, "Claw", cone, bone, start, Quaternion.FromToRotation(Vector3.up, clawDirection),
                    new Vector3(0.016f, Range(random, 0.2f, 0.28f), 0.016f) * unit, recordUndo);
            }
        }
    }

    // 등(가슴 뼈 뒤쪽)에서 위·뒤로 뻗어 꿈틀거리는 촉수
    private static void BuildTentacles(Transform zombie, Animator animator, float unit, Material material, System.Random random, bool recordUndo)
    {
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null)
        {
            chest = animator.GetBoneTransform(HumanBodyBones.Spine);
        }

        for (int i = 0; i < TentacleCount; i++)
        {
            float sideAmount = Mathf.Lerp(-1f, 1f, i / (float)(TentacleCount - 1));
            Vector3 origin = chest.position - zombie.forward * 0.09f * unit + zombie.up * Range(random, 0.02f, 0.14f) * unit
                + zombie.right * sideAmount * 0.11f * unit;
            Vector3 direction = (-zombie.forward * 0.7f + zombie.up * Range(random, 0.4f, 0.9f) + zombie.right * sideAmount * 0.5f).normalized;

            var tentacleObject = new GameObject(Prefix + "Tentacle");
            RegisterCreated(tentacleObject, recordUndo);
            Transform tentacle = tentacleObject.transform;
            tentacle.SetParent(chest, false);
            tentacle.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, zombie.up));
            SetWorldScale(tentacle, Vector3.one * unit);

            var component = tentacleObject.AddComponent<Tentacle>();
            tentacleObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            var serialized = new SerializedObject(component);
            serialized.FindProperty("_length").floatValue = Range(random, 0.55f, 0.95f);
            serialized.FindProperty("_baseRadius").floatValue = Range(random, 0.022f, 0.032f);
            serialized.FindProperty("_waveSpeed").floatValue = Range(random, 1.2f, 2.2f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ───────────────────────── 눈 ─────────────────────────

    // 눈 하나: 빛나는 흰자 + 세로로 찢어진 동공. 로컬 앞(Z)을 바라본다.
    private static Transform CreateEyeLocal(Transform parent, Vector3 localPosition, Quaternion localRotation, float size, Mesh sphere, Materials materials)
    {
        GameObject eye = CreateLocal(parent, "Eye", sphere, materials.Sclera, localPosition, localRotation, Vector3.one * size);
        DecorateEye(eye, sphere, materials);
        return eye.transform;
    }

    private static Transform CreateEyeWorld(Transform parent, Vector3 position, Quaternion rotation, float worldSize, Mesh sphere, Materials materials, bool recordUndo)
    {
        GameObject eye = CreatePart(parent, "Eye", sphere, materials.Sclera, position, rotation, Vector3.one * worldSize, recordUndo);
        DecorateEye(eye, sphere, materials);
        return eye.transform;
    }

    private static void DecorateEye(GameObject eye, Mesh sphere, Materials materials)
    {
        eye.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameObject pupil = CreateLocal(eye.transform, "Pupil", sphere, materials.Pupil, new Vector3(0f, 0f, 0.45f), Quaternion.identity, new Vector3(0.16f, 0.62f, 0.14f));
        pupil.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // ───────────────────────── 생성 도우미 ─────────────────────────

    // 부모 아래에 월드 위치·회전·크기로 부품을 만든다.
    private static GameObject CreatePart(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 worldScale, bool recordUndo)
    {
        var part = new GameObject(Prefix + name);
        RegisterCreated(part, recordUndo);
        part.transform.SetParent(parent, false);
        part.transform.SetPositionAndRotation(position, rotation);
        SetWorldScale(part.transform, worldScale);
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    // 부모(이미 만든 생성물) 아래에 로컬 값으로 부품을 만든다. 부모가 지워지면 함께 지워지므로 Undo 등록은 부모가 맡는다.
    private static GameObject CreateLocal(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        var part = new GameObject(Prefix + name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    private static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Vector3 parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(worldScale.x / parentScale.x, worldScale.y / parentScale.y, worldScale.z / parentScale.z);
    }

    private static void RegisterCreated(GameObject created, bool recordUndo)
    {
        if (recordUndo)
        {
            Undo.RegisterCreatedObjectUndo(created, "Build Grotesque Zombie");
        }
    }

    private static void RemoveGenerated(Transform zombie, bool recordUndo)
    {
        var targets = new List<GameObject>();
        foreach (Transform child in zombie.GetComponentsInChildren<Transform>(true))
        {
            bool generated = child.name.StartsWith(Prefix) && (child.parent == null || !child.parent.name.StartsWith(Prefix));
            if (generated || child.name == "CosmicAura")
            {
                targets.Add(child.gameObject);
            }
        }

        foreach (GameObject target in targets)
        {
            if (recordUndo)
            {
                Undo.DestroyObjectImmediate(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }

    private static void ApplySkin(GameObject zombie, Material skin, bool recordUndo)
    {
        foreach (SkinnedMeshRenderer renderer in zombie.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = skin;
            }

            if (recordUndo)
            {
                Undo.RecordObject(renderer, "Build Grotesque Zombie");
            }

            renderer.sharedMaterials = materials;
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

    // ───────────────────────── 재질·메시 ─────────────────────────

    private static Materials PrepareMaterials()
    {
        return new Materials
        {
            Skin = PrepareSkin(),
            // 너무 밝으면 블룸에 동공이 묻히므로 은은하게만 빛나게 한다.
            Sclera = LoadOrCreateLit("GrotesqueScleraMat", new Color(0.85f, 0.8f, 0.55f), 0.9f, new Color(1f, 0.8f, 0.3f) * 0.35f),
            Pupil = LoadOrCreateUnlit("GrotesquePupilMat", Color.black),
            MawInside = LoadOrCreateLit("GrotesqueMawMat", new Color(0.2f, 0.01f, 0.02f), 0.85f, new Color(0.15f, 0f, 0.01f)),
            Bone = LoadOrCreateLit("GrotesqueBoneMat", new Color(0.78f, 0.72f, 0.58f), 0.5f, null),
            Tentacle = LoadOrCreateLit("GrotesqueTentacleMat", new Color(0.42f, 0.36f, 0.38f), 0.75f, null),
        };
    }

    // 핏기 없는 창백한 살갗 + 군데군데 검붉은 공허로 찢긴 틈 + 희미한 병든 윤곽
    private static Material PrepareSkin()
    {
        Shader shader = Shader.Find(SkinShaderName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(SkinMaterialPath);
        if (material == null)
        {
            EnsureFolder(MaterialFolder);
            material = new Material(shader) { name = "CosmicZombieMat" };
            AssetDatabase.CreateAsset(material, SkinMaterialPath);
        }

        material.shader = shader;
        Texture skinTexture = FindZombieSkinTexture();
        if (skinTexture != null)
        {
            material.SetTexture("_BaseMap", skinTexture);
        }

        material.SetColor("_BaseColor", new Color(0.85f, 0.82f, 0.78f));
        material.SetFloat("_Pallor", 0.8f);
        material.SetFloat("_Smoothness", 0.55f);
        material.SetColor("_RimColor", new Color(0.35f, 0.4f, 0.15f));
        material.SetFloat("_RimPower", 4f);
        material.SetColor("_NebulaColorA", new Color(0.12f, 0f, 0.01f));
        material.SetColor("_NebulaColorB", new Color(0.02f, 0f, 0.03f));
        material.SetColor("_StarColor", new Color(2f, 1.6f, 1.4f));
        material.SetFloat("_StarDensity", 45f);
        material.SetFloat("_VoidAmount", 0.7f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture FindZombieSkinTexture()
    {
        foreach (string guid in AssetDatabase.FindAssets("Zombie t:Material"))
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (original != null && original.name == "Zombie" && original.HasProperty("_BaseMap") && original.GetTexture("_BaseMap") != null)
            {
                return original.GetTexture("_BaseMap");
            }
        }

        return null;
    }

    private static Material LoadOrCreateLit(string materialName, Color color, float smoothness, Color? emission)
    {
        Material material = LoadOrCreate(materialName, "Universal Render Pipeline/Lit");
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

    private static Material LoadOrCreateUnlit(string materialName, Color color)
    {
        Material material = LoadOrCreate(materialName, "Universal Render Pipeline/Unlit");
        material.SetColor("_BaseColor", color);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreate(string materialName, string shaderName)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    // 밑면 반지름 0.5, 높이 1인 원뿔 (밑면 중심이 원점, 위(Y)로 뾰족). 이빨과 발톱에 쓴다.
    private static Mesh LoadOrCreateCone()
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath);
        if (existing != null)
        {
            return existing;
        }

        const int segments = 10;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments;
            float a1 = (i + 1) * Mathf.PI * 2f / segments;
            float mid = (a0 + a1) * 0.5f;
            int start = vertices.Count;
            vertices.Add(new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f));
            vertices.Add(new Vector3(0f, 1f, 0f));
            vertices.Add(new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f));
            normals.Add(new Vector3(Mathf.Cos(a0), 0.5f, Mathf.Sin(a0)).normalized);
            normals.Add(new Vector3(Mathf.Cos(mid), 0.5f, Mathf.Sin(mid)).normalized);
            normals.Add(new Vector3(Mathf.Cos(a1), 0.5f, Mathf.Sin(a1)).normalized);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        var mesh = new Mesh { name = "GrotesqueCone" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        EnsureFolder(ConeMeshPath.Substring(0, ConeMeshPath.LastIndexOf('/')));
        AssetDatabase.CreateAsset(mesh, ConeMeshPath);
        return mesh;
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
