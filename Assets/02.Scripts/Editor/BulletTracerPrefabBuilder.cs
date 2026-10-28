using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 총알 궤적(레이저) 프리팹을 만든다. 다시 실행하면 같은 에셋을 갱신한다.
public static class BulletTracerPrefabBuilder
{
    private const string OutputDir = "Assets/03.Prefabs/Effects";
    private const string PrefabPath = OutputDir + "/BulletTracer.prefab";
    private const string MaterialPath = OutputDir + "/BulletTracer.mat";
    private const string UrpUnlitShader = "Universal Render Pipeline/Unlit";
    private static readonly Color LaserColor = new Color(1f, 0.25f, 0.1f, 1f);
    private const float Width = 0.04f;

    [MenuItem("Tools/Effects/Build Bullet Tracer Prefab")]
    private static void Build()
    {
        Shader shader = Shader.Find(UrpUnlitShader);

        if (shader == null)
        {
            Debug.LogError("URP Unlit 셰이더를 찾지 못했습니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputDir))
        {
            AssetDatabase.CreateFolder("Assets/03.Prefabs", "Effects");
        }

        Material material = FindOrCreateMaterial(shader);

        var tracerObject = new GameObject("BulletTracer", typeof(LineRenderer), typeof(BulletTracer));
        var line = tracerObject.GetComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.startWidth = Width;
        line.endWidth = Width * 0.3f;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.startColor = LaserColor;
        line.endColor = new Color(LaserColor.r, LaserColor.g, LaserColor.b, 0f);

        PrefabUtility.SaveAsPrefabAsset(tracerObject, PrefabPath);
        Object.DestroyImmediate(tracerObject);
        AssetDatabase.SaveAssets();
        Debug.Log($"총알 궤적 프리팹을 만들었습니다: {PrefabPath}");
    }

    private static Material FindOrCreateMaterial(Shader shader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        material.SetColor("_BaseColor", LaserColor);

        // 꼬리로 갈수록 투명해지도록 알파 블렌드로 설정한다.
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(material);
        return material;
    }
}
