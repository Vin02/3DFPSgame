using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 피 에셋(BloodSprayFX)을 URP에서 보이도록 변환한 총알 피격용 프리팹(BloodHit)을 만든다.
// 서드파티 원본은 수정하지 않고, 머티리얼과 프리팹을 03.Prefabs/Effects에 새로 만든다. 다시 실행하면 같은 에셋을 갱신한다.
public static class BloodHitPrefabBuilder
{
    private const string SourcePrefabPath = "Assets/BloodDecalsAndEffects/BloodGushFX/BloodSprayFX.prefab";
    private const string OutputDir = "Assets/03.Prefabs/Effects";
    private const string PrefabPath = OutputDir + "/BloodHit.prefab";
    private const string UrpParticleShader = "Universal Render Pipeline/Particles/Unlit";
    private const float BurstDuration = 0.35f;

    [MenuItem("Tools/Effects/Build Blood Hit Prefab")]
    private static void Build()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
        Shader shader = Shader.Find(UrpParticleShader);

        if (source == null || shader == null)
        {
            Debug.LogError("피 프리팹 또는 URP 파티클 셰이더를 찾지 못했습니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputDir))
        {
            AssetDatabase.CreateFolder("Assets/03.Prefabs", "Effects");
        }

        GameObject instance = Object.Instantiate(source);
        instance.name = "BloodHit";

        foreach (ParticleSystem particleSystem in instance.GetComponentsInChildren<ParticleSystem>())
        {
            ConfigureAsOneShot(particleSystem);

            var particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = FindOrCreateUrpMaterial(particleRenderer.sharedMaterial, shader);
        }

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        Debug.Log($"피격 피 프리팹을 만들었습니다: {PrefabPath}");
    }

    // 반복 재생을 끄고 짧게 한 번만 터지게 한다.
    private static void ConfigureAsOneShot(ParticleSystem particleSystem)
    {
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = false;
        main.duration = BurstDuration;
        main.playOnAwake = true;
    }

    private static Material FindOrCreateUrpMaterial(Material legacy, Shader shader)
    {
        string path = $"{OutputDir}/{legacy.name}_URP.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_BaseMap", legacy.GetTexture("_MainTex"));
        material.SetColor("_BaseColor", Color.white);

        // 알파 블렌드 투명 머티리얼로 설정한다.
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
