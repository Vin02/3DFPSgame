using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 맵에 쓰는 색(재질) 종류
public enum MapPaint
{
    Floor,
    Wall,
    Trim,
    Roof,
    Column,
    Hazard,
    RackPost,
    RackShelf,
    Crate,
    CrateBand,
    Cardboard,
    ContainerRed,
    ContainerGreen,
    Steel,
    Barrel,
    LampFixture,
    Window,
}

// 맵 재질을 Assets/09.Materials에서 불러오거나 새로 만든다. (기존 땅 재질과 같은 URP Lit 셰이더 사용)
public sealed class MapMaterialLibrary
{
    private const string MaterialFolder = "Assets/09.Materials";
    private const string ShaderTemplateMaterial = "GroundMat";

    private readonly struct PaintSpec
    {
        public readonly string MaterialName;
        public readonly Color Color;
        public readonly float EmissionIntensity;

        public PaintSpec(string materialName, Color color, float emissionIntensity = 0f)
        {
            MaterialName = materialName;
            Color = color;
            EmissionIntensity = emissionIntensity;
        }
    }

    private static readonly Dictionary<MapPaint, PaintSpec> Specs = new Dictionary<MapPaint, PaintSpec>
    {
        { MapPaint.Floor, new PaintSpec("WhFloorMat", new Color(0.42f, 0.42f, 0.44f)) },
        { MapPaint.Wall, new PaintSpec("WhWallMat", new Color(0.64f, 0.62f, 0.57f)) },
        { MapPaint.Trim, new PaintSpec("WhTrimMat", new Color(0.22f, 0.25f, 0.3f)) },
        { MapPaint.Roof, new PaintSpec("WhRoofMat", new Color(0.2f, 0.21f, 0.23f)) },
        { MapPaint.Column, new PaintSpec("WhColumnMat", new Color(0.5f, 0.5f, 0.52f)) },
        { MapPaint.Hazard, new PaintSpec("WhHazardMat", new Color(0.95f, 0.78f, 0.08f)) },
        { MapPaint.RackPost, new PaintSpec("WhRackPostMat", new Color(0.12f, 0.3f, 0.65f)) },
        { MapPaint.RackShelf, new PaintSpec("WhRackShelfMat", new Color(0.9f, 0.45f, 0.1f)) },
        { MapPaint.Crate, new PaintSpec("WhCrateMat", new Color(0.55f, 0.38f, 0.2f)) },
        { MapPaint.CrateBand, new PaintSpec("WhCrateBandMat", new Color(0.3f, 0.2f, 0.1f)) },
        { MapPaint.Cardboard, new PaintSpec("WhCardboardMat", new Color(0.72f, 0.58f, 0.38f)) },
        { MapPaint.ContainerRed, new PaintSpec("WhContainerRedMat", new Color(0.6f, 0.15f, 0.12f)) },
        { MapPaint.ContainerGreen, new PaintSpec("WhContainerGreenMat", new Color(0.15f, 0.4f, 0.27f)) },
        { MapPaint.Steel, new PaintSpec("WhSteelMat", new Color(0.3f, 0.32f, 0.35f)) },
        { MapPaint.Barrel, new PaintSpec("WhBarrelMat", new Color(0.7f, 0.12f, 0.1f)) },
        { MapPaint.LampFixture, new PaintSpec("WhLampMat", new Color(1f, 0.95f, 0.8f), 3f) },
        { MapPaint.Window, new PaintSpec("WhWindowMat", new Color(0.7f, 0.85f, 1f), 1.5f) },
    };

    private readonly Dictionary<MapPaint, Material> _materials = new Dictionary<MapPaint, Material>();

    public static MapMaterialLibrary Prepare(string undoGroupName)
    {
        var library = new MapMaterialLibrary();

        foreach (KeyValuePair<MapPaint, PaintSpec> spec in Specs)
        {
            Material material = LoadOrCreateMaterial(spec.Value.MaterialName);

            Undo.RecordObject(material, undoGroupName);
            Apply(material, spec.Value);
            EditorUtility.SetDirty(material);

            library._materials.Add(spec.Key, material);
        }

        return library;
    }

    public Material Get(MapPaint paint)
    {
        return _materials[paint];
    }

    private static Material LoadOrCreateMaterial(string materialName)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
        {
            return material;
        }

        var template = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{ShaderTemplateMaterial}.mat");
        material = new Material(template.shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void Apply(Material material, PaintSpec spec)
    {
        SetColor(material, "_BaseColor", spec.Color);
        SetColor(material, "_Color", spec.Color);

        if (spec.EmissionIntensity > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", spec.Color * spec.EmissionIntensity);
        }
    }

    private static void SetColor(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }
}
