using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 메뉴 한 번으로 Player의 겉모습을 닭으로 만든다.
// 씬 파일은 저장하지 않는다. 결과를 확인한 뒤 직접 저장한다. (Ctrl+Z로 되돌릴 수 있다)
public static class ChickenCharacterBuilder
{
    private const string MaterialFolder = "Assets/09.Materials";
    private const string UndoGroupName = "Make Chicken";

    private enum Skin
    {
        Body,
        Eye,
        Beak,
        Wing,
        Comb,
    }

    private readonly struct SkinSpec
    {
        public readonly string MaterialName;
        public readonly Color Color;

        public SkinSpec(string materialName, Color color)
        {
            MaterialName = materialName;
            Color = color;
        }
    }

    private readonly struct PartSpec
    {
        public readonly string Name;
        public readonly PrimitiveType Shape;
        public readonly Skin Skin;
        public readonly Vector3 Position;
        public readonly Vector3 EulerAngles;
        public readonly Vector3 Scale;

        public PartSpec(string name, PrimitiveType shape, Skin skin,
            Vector3 position, Vector3 eulerAngles, Vector3 scale)
        {
            Name = name;
            Shape = shape;
            Skin = skin;
            Position = position;
            EulerAngles = eulerAngles;
            Scale = scale;
        }
    }

    private static readonly Dictionary<Skin, SkinSpec> Skins = new Dictionary<Skin, SkinSpec>
    {
        { Skin.Body, new SkinSpec("PlayerMat", new Color(0.97f, 0.97f, 0.95f)) },
        { Skin.Eye, new SkinSpec("PlayerEyesMat", Color.black) },
        { Skin.Beak, new SkinSpec("PlayerNoseMat", new Color(1f, 0.6f, 0.05f)) },
        { Skin.Wing, new SkinSpec("PlayerWIngMat", new Color(0.95f, 0.88f, 0.72f)) },
        { Skin.Comb, new SkinSpec("PlayerCombMat", new Color(0.9f, 0.08f, 0.08f)) },
    };

    // 플레이어는 +Z를 바라본다. 몸통은 반지름 0.5, 높이 2인 캡슐이다.
    private static readonly PartSpec[] Parts =
    {
        // 눈
        new PartSpec("eyeL", PrimitiveType.Sphere, Skin.Eye, new Vector3(0.28f, 0.55f, 0.41f), Vector3.zero, Vector3.one * 0.12f),
        new PartSpec("eyeR", PrimitiveType.Sphere, Skin.Eye, new Vector3(-0.28f, 0.55f, 0.41f), Vector3.zero, Vector3.one * 0.12f),

        // 부리(윗부리 nose, 아랫부리 mouth)와 육수
        new PartSpec("nose", PrimitiveType.Capsule, Skin.Beak, new Vector3(0f, 0.3f, 0.57f), new Vector3(90f, 0f, 0f), new Vector3(0.16f, 0.11f, 0.16f)),
        new PartSpec("mouth", PrimitiveType.Capsule, Skin.Beak, new Vector3(0f, 0.2f, 0.53f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.08f, 0.12f)),
        new PartSpec("wattle", PrimitiveType.Sphere, Skin.Comb, new Vector3(0f, 0.06f, 0.49f), Vector3.zero, new Vector3(0.1f, 0.2f, 0.08f)),

        // 닭벼슬
        new PartSpec("combFront", PrimitiveType.Sphere, Skin.Comb, new Vector3(0f, 0.98f, 0.16f), Vector3.zero, new Vector3(0.1f, 0.2f, 0.2f)),
        new PartSpec("combMiddle", PrimitiveType.Sphere, Skin.Comb, new Vector3(0f, 1.06f, 0f), Vector3.zero, new Vector3(0.1f, 0.28f, 0.24f)),
        new PartSpec("combBack", PrimitiveType.Sphere, Skin.Comb, new Vector3(0f, 0.98f, -0.16f), Vector3.zero, new Vector3(0.1f, 0.2f, 0.2f)),

        // 날개
        new PartSpec("wingL", PrimitiveType.Sphere, Skin.Wing, new Vector3(0.54f, 0f, -0.05f), new Vector3(0f, 0f, 12f), new Vector3(0.12f, 0.7f, 0.5f)),
        new PartSpec("wingR", PrimitiveType.Sphere, Skin.Wing, new Vector3(-0.54f, 0f, -0.05f), new Vector3(0f, 0f, -12f), new Vector3(0.12f, 0.7f, 0.5f)),

        // 꼬리깃
        new PartSpec("tailCenter", PrimitiveType.Sphere, Skin.Wing, new Vector3(0f, 0.3f, -0.64f), new Vector3(-35f, 0f, 0f), new Vector3(0.08f, 0.5f, 0.22f)),
        new PartSpec("tailLeft", PrimitiveType.Sphere, Skin.Wing, new Vector3(0.1f, 0.28f, -0.62f), new Vector3(-35f, 0f, -20f), new Vector3(0.08f, 0.5f, 0.22f)),
        new PartSpec("tailRight", PrimitiveType.Sphere, Skin.Wing, new Vector3(-0.1f, 0.28f, -0.62f), new Vector3(-35f, 0f, 20f), new Vector3(0.08f, 0.5f, 0.22f)),

        // 발
        new PartSpec("feetL", PrimitiveType.Sphere, Skin.Beak, new Vector3(0.25f, -0.94f, 0.15f), Vector3.zero, new Vector3(0.22f, 0.12f, 0.5f)),
        new PartSpec("feetR", PrimitiveType.Sphere, Skin.Beak, new Vector3(-0.25f, -0.94f, 0.15f), Vector3.zero, new Vector3(0.22f, 0.12f, 0.5f)),
    };

    [MenuItem("Tools/Player/Make Chicken")]
    private static void MakeChicken()
    {
        PlayerMove player = Object.FindFirstObjectByType<PlayerMove>();

        if (player == null)
        {
            Debug.LogError("씬에서 PlayerMove가 붙은 Player를 찾지 못했습니다.");
            return;
        }

        Undo.SetCurrentGroupName(UndoGroupName);
        int undoGroup = Undo.GetCurrentGroup();

        Dictionary<Skin, Material> materials = PrepareMaterials();
        RenameLegacyPart(player.transform, "mouse", "mouth");
        ApplyBodySkin(player, materials[Skin.Body]);

        foreach (PartSpec spec in Parts)
        {
            ApplyPart(player.transform, spec, materials[spec.Skin]);
        }

        if (player.GetComponent<WingFlapper>() == null)
        {
            Undo.AddComponent<WingFlapper>(player.gameObject);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = player.gameObject;
    }

    private static Dictionary<Skin, Material> PrepareMaterials()
    {
        var materials = new Dictionary<Skin, Material>();

        foreach (KeyValuePair<Skin, SkinSpec> pair in Skins)
        {
            Material material = LoadOrCreateMaterial(pair.Value.MaterialName);
            Undo.RecordObject(material, UndoGroupName);
            SetColor(material, pair.Value.Color);
            EditorUtility.SetDirty(material);
            materials.Add(pair.Key, material);
        }

        return materials;
    }

    private static Material LoadOrCreateMaterial(string materialName)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
        {
            return material;
        }

        // 새 재질은 기존 몸통 재질과 같은 셰이더(URP Lit)를 쓴다.
        Material template = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{Skins[Skin.Body].MaterialName}.mat");
        material = new Material(template.shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void RenameLegacyPart(Transform player, string oldName, string newName)
    {
        Transform legacy = player.Find(oldName);

        if (legacy == null || player.Find(newName) != null)
        {
            return;
        }

        Undo.RecordObject(legacy.gameObject, UndoGroupName);
        legacy.name = newName;
    }

    private static void ApplyBodySkin(PlayerMove player, Material material)
    {
        var bodyRenderer = player.GetComponent<MeshRenderer>();
        Undo.RecordObject(bodyRenderer, UndoGroupName);
        bodyRenderer.sharedMaterial = material;
    }

    private static void ApplyPart(Transform player, PartSpec spec, Material material)
    {
        Transform part = GetOrCreatePart(player, spec);

        // 자식 콜라이더는 CharacterController 이동을 막을 수 있어 제거한다.
        foreach (Collider partCollider in part.GetComponents<Collider>())
        {
            Undo.DestroyObjectImmediate(partCollider);
        }

        var meshFilter = part.GetComponent<MeshFilter>();
        Undo.RecordObject(meshFilter, UndoGroupName);
        meshFilter.sharedMesh = Resources.GetBuiltinResource<Mesh>($"{spec.Shape}.fbx");

        var partRenderer = part.GetComponent<MeshRenderer>();
        Undo.RecordObject(partRenderer, UndoGroupName);
        partRenderer.sharedMaterial = material;

        Undo.RecordObject(part, UndoGroupName);
        part.localPosition = spec.Position;
        part.localRotation = Quaternion.Euler(spec.EulerAngles);
        part.localScale = spec.Scale;
    }

    private static Transform GetOrCreatePart(Transform player, PartSpec spec)
    {
        Transform part = player.Find(spec.Name);

        if (part != null)
        {
            return part;
        }

        GameObject created = GameObject.CreatePrimitive(spec.Shape);
        Undo.RegisterCreatedObjectUndo(created, UndoGroupName);
        created.name = spec.Name;
        created.transform.SetParent(player, false);
        return created.transform;
    }
}
