using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// 글리치 화면 효과(Custom/ColossusGlitch)를 URP 렌더러의 전체 화면 패스로 등록한다.
// PC·모바일 렌더러에 모두 넣고, 이미 있으면 다시 넣지 않는다. 세기가 0이면 화면을 그대로 통과시킨다.
public static class ColossusGlitchInstaller
{
    private const string FeatureName = "ColossusGlitch";
    private const string MaterialFolder = "Assets/09.Materials/Monsters";
    private const string MaterialPath = MaterialFolder + "/ColossusGlitchMat.mat";
    private const string ShaderName = "Custom/ColossusGlitch";

    private static readonly string[] RendererPaths =
    {
        "Assets/Settings/PC_Renderer.asset",
        "Assets/Settings/Mobile_Renderer.asset",
    };

    [MenuItem("Tools/Monsters/Install Colossus Glitch")]
    public static void Install()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"{ShaderName} 셰이더를 찾지 못했습니다.");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "ColossusGlitchMat" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        EditorUtility.SetDirty(material);

        foreach (string path in RendererPaths)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
            if (data == null)
            {
                Debug.LogWarning($"{path}를 찾지 못해 건너뜁니다.");
                continue;
            }

            AddFeature(data, material);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("글리치 전체 화면 패스를 렌더러에 등록했습니다.");
    }

    private static void AddFeature(ScriptableRendererData data, Material material)
    {
        foreach (ScriptableRendererFeature existing in data.rendererFeatures)
        {
            if (existing != null && existing.name == FeatureName)
            {
                Configure((FullScreenPassRendererFeature)existing, material);
                EditorUtility.SetDirty(existing);
                return;
            }
        }

        var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = FeatureName;
        Configure(feature, material);
        AssetDatabase.AddObjectToAsset(feature, data);

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
        {
            Debug.LogError("렌더러 기능의 파일 ID를 얻지 못했습니다.");
            return;
        }

        var serialized = new SerializedObject(data);
        SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
        SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
        int index = features.arraySize;
        features.arraySize = index + 1;
        features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
        map.arraySize = index + 1;
        map.GetArrayElementAtIndex(index).longValue = localId;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static void Configure(FullScreenPassRendererFeature feature, Material material)
    {
        feature.passMaterial = material;
        feature.passIndex = 0;
        feature.fetchColorBuffer = true;
        feature.requirements = ScriptableRenderPassInput.None;
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
    }
}
