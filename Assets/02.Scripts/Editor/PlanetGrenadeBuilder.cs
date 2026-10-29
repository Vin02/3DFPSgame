using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

// "Planets of the Solar System 3D"의 행성 10개로 수류탄 프리팹을 만들고, 플레이어의 PlayerFlashbangThrow에 던지는 순서대로 연결한다.
// 순서: 태양 → 수성 → 금성 → 지구 → 달 → 화성 → 목성 → 토성 → 천왕성 → 해왕성
// 행성 본체(구)의 지름이 기존 섬광탄 지름(0.08m)의 2배인 0.16m가 되도록 크기를 맞춘다.
// 동작(불이 붙어 밝히고 주변에 피해)은 기존 섬광탄 프리팹의 FlashbangProjectile 설정을 그대로 복사해 쓴다.
// 이름으로 찾아 덮어쓰므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다.
public static class PlanetGrenadeBuilder
{
    private const string SourceFolder = "Assets/Planets of the Solar System 3D/Prefabs/";
    private const string OutputFolder = "Assets/03.Prefabs/Weapons/Planets";
    private const string FlashbangPrefabPath = "Assets/03.Prefabs/Weapons/Flashbang.prefab";
    private const float Diameter = 0.16f; // 기존 섬광탄 지름 0.08m의 2배
    private const float ColliderGrow = 1f; // 충돌 구는 행성 본체와 같은 크기

    // 던지는 순서대로: (프리팹 이름, 행성 본체 오브젝트 이름, 불빛 색, 불빛이 오갈 두 번째 색 — 없으면 한 색만)
    private static readonly (string prefab, string body, Color light, Color? lightAlt)[] Planets =
    {
        ("Sun", "Sphere", new Color(1f, 0.08f, 0.05f), null),       // 1 빨강
        ("Mercury", "Mercury", new Color(1f, 0.48f, 0.05f), null),  // 2 주황
        ("Venus", "Venus", new Color(1f, 0.92f, 0.15f), null),      // 3 노랑
        ("Earth", "Earth", new Color(0.15f, 1f, 0.25f), null),      // 4 초록
        ("Moon", "Moon", new Color(0.1f, 0.3f, 1f), null),          // 5 파랑
        ("Mars", "Mars", new Color(1f, 0.08f, 0.05f), null),        // 6 빨강
        ("Jupiter", "Jupiter", new Color(1f, 0.92f, 0.15f), null),  // 7 노랑
        ("Saturn", "Saturn", new Color(0.15f, 1f, 0.25f), null),    // 8 초록
        ("Uranus", "Uranus", new Color(0.1f, 0.3f, 1f), null),      // 9 파랑
        ("Neptune", "Neptune", new Color(0.6f, 0.1f, 1f), null),    // 10 보라
    };

    private const float ExplosionMultiplier = 50f; // 사라지기 전 폭발에서 행성 크기와 피해 반경이 커지는 배율
    private const float ColorShiftSpeed = 1.2f; // 두 색을 오가는 빠르기(라디안/초). 한 번 오가는 데 약 5초

    private const string HovlFolder = "Assets/Hovl Studio/Magic effects pack/Prefabs/";
    private const string UnityFxFolder = "Assets/UnityTechnologies/ParticlePack/EffectExamples/Fire & Explosion Effects/Prefabs/";

    // 폭발 때 터뜨릴 이펙트: 행성 불빛 색과 같은 계열. (프리팹 경로, 크기 — 부푼 행성 반지름 4m 기준)
    private static readonly Dictionary<string, (string path, float scale)[]> Effects = new Dictionary<string, (string, float)[]>
    {
        // 빨강
        ["Sun"] = new[] { (HovlFolder + "AoE effects/Red energy explosion.prefab", 1.2f), (HovlFolder + "Sparks/Sparks explode red.prefab", 3f), (HovlFolder + "Sparks/Sparks flashing red.prefab", 3f) },
        // 주황
        ["Mercury"] = new[] { (UnityFxFolder + "BigExplosion.prefab", 1.2f), (UnityFxFolder + "SmallExplosion.prefab", 2.5f), (HovlFolder + "Sparks/Sparks explode yellow.prefab", 3f) },
        // 노랑
        ["Venus"] = new[] { (UnityFxFolder + "FireBall.prefab", 1.5f), (HovlFolder + "Hits and explosions/Holy hit.prefab", 3f), (HovlFolder + "Sparks/Sparks explode yellow.prefab", 3f) },
        // 초록
        ["Earth"] = new[] { (HovlFolder + "Hits and explosions/Green hit.prefab", 3f), (HovlFolder + "AoE effects/AoE slash green.prefab", 1f), (HovlFolder + "Sparks/Sparks explode green.prefab", 3f) },
        // 파랑
        ["Moon"] = new[] { (UnityFxFolder + "EnergyExplosion.prefab", 3f), (HovlFolder + "Hits and explosions/Electro hit.prefab", 3f), (HovlFolder + "Sparks/Sparks explode blue.prefab", 3f) },
        // 빨강
        ["Mars"] = new[] { (HovlFolder + "Sparks/Sparks explode red.prefab", 4f), (HovlFolder + "Sparks/Sparks flashing red.prefab", 4f), (HovlFolder + "AoE effects/Red energy explosion.prefab", 0.8f) },
        // 노랑
        ["Jupiter"] = new[] { (HovlFolder + "Hits and explosions/Star hit.prefab", 3f), (HovlFolder + "Sparks/Sparks flashing yellow.prefab", 4f), (HovlFolder + "Sparks/Sparks explode yellow.prefab", 4f) },
        // 초록
        ["Saturn"] = new[] { (HovlFolder + "AoE effects/AoE slash green.prefab", 1f), (HovlFolder + "Sparks/Sparks flashing green.prefab", 4f), (HovlFolder + "Sparks/Sparks explode green.prefab", 4f) },
        // 파랑
        ["Uranus"] = new[] { (HovlFolder + "Hits and explosions/Snow hit.prefab", 3f), (HovlFolder + "Sparks/Sparks flashing blue.prefab", 4f), (HovlFolder + "AoE effects/AoE slash blue.prefab", 1f) },
        // 보라(분홍 계열)
        ["Neptune"] = new[] { (HovlFolder + "Sparks/Sparks explode pink.prefab", 4f), (HovlFolder + "Sparks/Sparks flashing pink.prefab", 4f), (HovlFolder + "Hits and explosions/Love hit.prefab", 3f) },
    };

    [MenuItem("Tools/Weapons/Build Planet Grenades")]
    private static void Build()
    {
        var flashbang = AssetDatabase.LoadAssetAtPath<GameObject>(FlashbangPrefabPath);
        PlayerFlashbangThrow thrower = Object.FindFirstObjectByType<PlayerFlashbangThrow>();
        if (flashbang == null || thrower == null)
        {
            Debug.LogError("Flashbang 프리팹 또는 씬의 PlayerFlashbangThrow를 찾지 못했습니다. (Tools > Weapons > Build Flashbang)");
            return;
        }

        EnsureFolder(OutputFolder);
        var prefabs = new FlashbangProjectile[Planets.Length];
        for (int i = 0; i < Planets.Length; i++)
        {
            prefabs[i] = BuildOne(Planets[i], flashbang);
            if (prefabs[i] == null)
            {
                return;
            }
        }

        var serialized = new SerializedObject(thrower);
        SerializedProperty array = serialized.FindProperty("_grenadePrefabs");
        array.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++)
        {
            array.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        }

        serialized.FindProperty("_maxBombs").intValue = prefabs.Length;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(thrower.gameObject.scene);

        AssetDatabase.SaveAssets();
        Debug.Log($"행성 수류탄 {prefabs.Length}개를 만들고 연결했습니다. 결과를 확인한 뒤 씬을 저장하세요.");
    }

    private static FlashbangProjectile BuildOne((string prefab, string body, Color light, Color? lightAlt) planet, GameObject flashbang)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFolder + planet.prefab + ".prefab");
        if (source == null)
        {
            Debug.LogError($"{SourceFolder}{planet.prefab}.prefab을 찾지 못했습니다.");
            return null;
        }

        var root = new GameObject(planet.prefab + "Grenade");
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // 충돌은 새로 만드는 구 하나로 한다. 원본의 콜라이더는 지운다.
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            // 행성 본체 구의 크기·중심을 재서, 지름이 Diameter가 되고 중심이 루트 원점에 오도록 맞춘다.
            Transform bodyTransform = FindDeep(visual.transform, planet.body);
            var bodyRenderer = bodyTransform != null ? bodyTransform.GetComponent<MeshRenderer>() : null;
            if (bodyRenderer == null)
            {
                Debug.LogError($"{planet.prefab}: 행성 본체({planet.body})의 MeshRenderer를 찾지 못했습니다.");
                return null;
            }

            Bounds bounds = bodyRenderer.bounds;
            float scale = Diameter / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = -(bounds.center - visual.transform.position) * scale;

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 빛이 행성 안에서 나오므로 자신의 그림자는 끈다.
            }

            // 물리: 섬광탄 프리팹의 설정을 그대로 가져온다.
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = Diameter * 0.5f * ColliderGrow;
            var flashbangCollider = flashbang.GetComponent<Collider>();
            if (flashbangCollider != null)
            {
                sphere.sharedMaterial = flashbangCollider.sharedMaterial;
            }

            CopyComponent<Rigidbody>(flashbang, root);

            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root.transform, false);
            var light = lightObject.AddComponent<Light>();
            Light sourceLight = flashbang.GetComponentInChildren<Light>(true);
            light.type = LightType.Point;
            light.range = sourceLight != null ? sourceLight.range : 32f;
            light.shadows = sourceLight != null ? sourceLight.shadows : LightShadows.Soft;
            light.shadowNearPlane = 0.1f;
            light.color = planet.light;
            light.intensity = 0f;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.enabled = false;

            FlashbangProjectile projectile = CopyComponent<FlashbangProjectile>(flashbang, root);
            var serialized = new SerializedObject(projectile);
            serialized.FindProperty("_light").objectReferenceValue = light;
            serialized.FindProperty("_glowRenderer").objectReferenceValue = null;
            serialized.FindProperty("_displayName").stringValue = planet.prefab;
            AssignEffects(serialized, planet.prefab);
            serialized.FindProperty("_explosionRadiusMultiplier").floatValue = ExplosionMultiplier;
            serialized.FindProperty("_lightColor").colorValue = planet.light;
            serialized.FindProperty("_lightColorAlt").colorValue = planet.lightAlt ?? planet.light;
            serialized.FindProperty("_colorShiftSpeed").floatValue = planet.lightAlt.HasValue ? ColorShiftSpeed : 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{OutputFolder}/Planet_{planet.prefab}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            return saved.GetComponent<FlashbangProjectile>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void AssignEffects(SerializedObject serialized, string planetName)
    {
        if (!Effects.TryGetValue(planetName, out (string path, float scale)[] entries))
        {
            return;
        }

        SerializedProperty array = serialized.FindProperty("_explosionEffects");
        array.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entries[i].path);
            if (prefab == null)
            {
                Debug.LogError($"{planetName}: 이펙트 프리팹을 찾지 못했습니다: {entries[i].path}");
            }

            SerializedProperty element = array.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("Scale").floatValue = entries[i].scale;
        }
    }

    // 원본 오브젝트의 컴포넌트를 값까지 그대로 복사해 대상에 붙인다.
    private static T CopyComponent<T>(GameObject from, GameObject to) where T : Component
    {
        ComponentUtility.CopyComponent(from.GetComponent<T>());
        ComponentUtility.PasteComponentAsNew(to);
        return to.GetComponent<T>();
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name && child.GetComponent<MeshRenderer>() != null)
            {
                return child;
            }
        }

        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
