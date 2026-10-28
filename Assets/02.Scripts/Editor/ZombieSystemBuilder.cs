using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 씬의 좀비를 바탕으로 모델별 좀비 프리팹(Zombie1~3)을 만들고, ZombieManager와 스포너를 씬에 배치한다.
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다. (Ctrl+Z로 되돌릴 수 있다)
public static class ZombieSystemBuilder
{
    private const string PrefabDir = "Assets/03.Prefabs/Monsters";
    private const string ManagerName = "ZombieManager";
    private const string SpawnersName = "Spawners";
    private const string ModelName = "Model";
    private static readonly string[] ModelPrefabPaths =
    {
        "Assets/Zombie/Prefabs/Zombie1.prefab",
        "Assets/Zombie/Prefabs/Zombie2.prefab",
        "Assets/Zombie/Prefabs/Zombie3.prefab",
    };

    [MenuItem("Tools/Monsters/Build Zombie Manager")]
    private static void Build()
    {
        Zombie[] sceneZombies = Object.FindObjectsByType<Zombie>(FindObjectsSortMode.InstanceID);
        GameObject minimapCamera = GameObject.Find("MinimapCamera");

        if (sceneZombies.Length == 0)
        {
            Debug.LogError("씬에 기준이 될 좀비가 없습니다.");
            return;
        }

        Undo.SetCurrentGroupName("Build Zombie Manager");
        int undoGroup = Undo.GetCurrentGroup();

        Zombie[] prefabs = BuildPrefabs(FindTemplate(sceneZombies));
        ZombieSpawner[] spawners = BuildSpawners(sceneZombies);
        BuildManager(prefabs, spawners, sceneZombies, minimapCamera != null ? minimapCamera.transform : null);

        EditorSceneManager.MarkAllScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);
    }

    // Model 자식이 있는 좀비를 기준으로 삼는다.
    private static Zombie FindTemplate(Zombie[] sceneZombies)
    {
        foreach (Zombie zombie in sceneZombies)
        {
            if (zombie.transform.Find(ModelName) != null)
            {
                return zombie;
            }
        }

        return sceneZombies[0];
    }

    // 기준 좀비를 복제해 모델만 바꾼 프리팹 3종을 만든다.
    private static Zombie[] BuildPrefabs(Zombie template)
    {
        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            AssetDatabase.CreateFolder("Assets/03.Prefabs", "Monsters");
        }

        var result = new Zombie[ModelPrefabPaths.Length];

        for (int i = 0; i < ModelPrefabPaths.Length; i++)
        {
            GameObject copy = Object.Instantiate(template.gameObject);
            copy.name = $"Zombie_Enemy_{i + 1}";
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            ReplaceModel(copy.transform, AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPaths[i]));
            ClearSceneReferences(copy);

            string path = $"{PrefabDir}/{copy.name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(copy, path);
            Object.DestroyImmediate(copy);
            result[i] = saved.GetComponent<Zombie>();
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    private static void ReplaceModel(Transform root, GameObject modelPrefab)
    {
        Transform oldModel = root.Find(ModelName);
        Vector3 localPosition = Vector3.zero;
        Quaternion localRotation = Quaternion.identity;
        RuntimeAnimatorController controller = null;

        if (oldModel != null)
        {
            localPosition = oldModel.localPosition;
            localRotation = oldModel.localRotation;
            controller = oldModel.GetComponent<Animator>().runtimeAnimatorController;
            Object.DestroyImmediate(oldModel.gameObject);
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root);
        model.name = ModelName;
        model.transform.localPosition = localPosition;
        model.transform.localRotation = localRotation;

        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
        {
            animator = model.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        model.AddComponent<ZombieAnimation>();
    }

    // 프리팹에는 씬 오브젝트 참조(미니맵 카메라)를 남기지 않는다. 생성할 때 ZombieManager가 넣어준다.
    private static void ClearSceneReferences(GameObject zombie)
    {
        foreach (MinimapIcon icon in zombie.GetComponentsInChildren<MinimapIcon>(true))
        {
            icon.Initialize(null);
        }
    }

    // 씬에 있던 좀비 위치마다, 그리고 그 중간 지점에 스포너를 둔다.
    private static ZombieSpawner[] BuildSpawners(Zombie[] sceneZombies)
    {
        GameObject manager = FindOrCreateManagerObject();
        Transform parent = FindOrCreateChild(manager.transform, SpawnersName);

        int count = sceneZombies.Length + (sceneZombies.Length > 1 ? 1 : 0);
        var spawners = new ZombieSpawner[count];

        for (int i = 0; i < sceneZombies.Length; i++)
        {
            spawners[i] = FindOrCreateSpawner(parent, $"Spawner_{i + 1}", sceneZombies[i].transform.position);
        }

        if (sceneZombies.Length > 1)
        {
            Vector3 middle = (sceneZombies[0].transform.position + sceneZombies[1].transform.position) * 0.5f;
            spawners[count - 1] = FindOrCreateSpawner(parent, $"Spawner_{count}", middle);
        }

        return spawners;
    }

    private static ZombieSpawner FindOrCreateSpawner(Transform parent, string name, Vector3 position)
    {
        Transform child = FindOrCreateChild(parent, name);
        child.position = position;

        ZombieSpawner spawner = child.GetComponent<ZombieSpawner>();
        return spawner != null ? spawner : Undo.AddComponent<ZombieSpawner>(child.gameObject);
    }

    private static void BuildManager(Zombie[] prefabs, ZombieSpawner[] spawners, Zombie[] existing, Transform minimapCamera)
    {
        GameObject managerObject = FindOrCreateManagerObject();
        ZombieManager manager = managerObject.GetComponent<ZombieManager>();
        if (manager == null)
        {
            manager = Undo.AddComponent<ZombieManager>(managerObject);
        }

        var serialized = new SerializedObject(manager);
        AssignArray(serialized.FindProperty("_zombiePrefabs"), prefabs);
        AssignArray(serialized.FindProperty("_spawners"), spawners);
        AssignArray(serialized.FindProperty("_existingZombies"), existing);
        serialized.FindProperty("_minimapCamera").objectReferenceValue = minimapCamera;
        serialized.ApplyModifiedProperties();
    }

    private static void AssignArray(SerializedProperty property, Object[] items)
    {
        property.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    private static GameObject FindOrCreateManagerObject()
    {
        GameObject manager = GameObject.Find(ManagerName);

        if (manager == null)
        {
            manager = new GameObject(ManagerName);
            Undo.RegisterCreatedObjectUndo(manager, "Create ZombieManager");
        }

        return manager;
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);

        if (child == null)
        {
            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            created.transform.SetParent(parent, false);
            child = created.transform;
        }

        return child;
    }
}
