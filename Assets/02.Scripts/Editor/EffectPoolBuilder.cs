using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 총알 이펙트용 오브젝트 풀(피격·피·궤적)을 씬에 만들고 PlayerGunFire에 연결한다.
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다. (Ctrl+Z로 되돌릴 수 있다)
public static class EffectPoolBuilder
{
    private const string RootName = "Pools";
    private const string HitPrefabPath = "Assets/Workshop/Effects/Stone_BulletImpact.prefab";
    private const string BloodPrefabPath = "Assets/03.Prefabs/Effects/BloodHit.prefab";
    private const string TracerPrefabPath = "Assets/03.Prefabs/Effects/BulletTracer.prefab";

    [MenuItem("Tools/Pools/Build Effect Pools")]
    private static void Build()
    {
        GameObject player = GameObject.Find("Player");
        PlayerGunFire gunFire = player != null ? player.GetComponent<PlayerGunFire>() : null;

        if (gunFire == null)
        {
            Debug.LogError("Player 또는 PlayerGunFire를 찾지 못했습니다.");
            return;
        }

        Undo.SetCurrentGroupName("Build Effect Pools");
        int undoGroup = Undo.GetCurrentGroup();

        Transform root = FindOrCreateRoot();
        EffectPool hit = BuildPool(root, "HitEffectPool", HitPrefabPath, 10, 30, 1.5f);
        EffectPool blood = BuildPool(root, "BloodEffectPool", BloodPrefabPath, 10, 30, 2f);
        // 궤적은 날아가는 시간이 거리에 따라 달라 BulletTracer가 끝나면 스스로 반환한다.
        EffectPool tracer = BuildPool(root, "TracerPool", TracerPrefabPath, 15, 40, 0f);

        var serialized = new SerializedObject(gunFire);
        serialized.FindProperty("_hitEffectPool").objectReferenceValue = hit;
        serialized.FindProperty("_bloodEffectPool").objectReferenceValue = blood;
        serialized.FindProperty("_tracerPool").objectReferenceValue = tracer;
        serialized.ApplyModifiedProperties();

        EditorSceneManager.MarkAllScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static Transform FindOrCreateRoot()
    {
        GameObject root = GameObject.Find(RootName);

        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Pools");
        }

        return root.transform;
    }

    private static EffectPool BuildPool(Transform root, string name, string prefabPath, int capacity, int maxSize, float lifetime)
    {
        Transform child = root.Find(name);
        if (child == null)
        {
            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            created.transform.SetParent(root, false);
            child = created.transform;
        }

        EffectPool pool = child.GetComponent<EffectPool>();
        if (pool == null)
        {
            pool = Undo.AddComponent<EffectPool>(child.gameObject);
        }

        var serialized = new SerializedObject(pool);
        serialized.FindProperty("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        serialized.FindProperty("_defaultCapacity").intValue = capacity;
        serialized.FindProperty("_maxSize").intValue = maxSize;
        serialized.FindProperty("_lifetime").floatValue = lifetime;
        serialized.ApplyModifiedProperties();
        return pool;
    }
}
