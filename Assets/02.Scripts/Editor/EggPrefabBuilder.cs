using UnityEditor;
using UnityEngine;

// 메뉴 한 번으로 계란 프리팹(Assets/03.Prefabs/Egg.prefab)을 만든다.
// 씬의 오브젝트는 건드리지 않는다. 다시 실행하면 같은 프리팹을 덮어쓴다.
public static class EggPrefabBuilder
{
    private const string PrefabFolder = "Assets/03.Prefabs";
    private const string MeshPath = PrefabFolder + "/EggMesh.asset";
    private const string PrefabPath = PrefabFolder + "/Egg.prefab";
    private const string MaterialPath = "Assets/09.Materials/EggMat.mat";
    private const float EggHeight = 1f;
    private const float EggWidth = 0.75f;

    [MenuItem("Tools/Prefabs/Create Egg")]
    private static void CreateEgg()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            Debug.LogError($"계란 재질을 찾지 못했습니다: {MaterialPath}");
            return;
        }

        Mesh mesh = LoadOrCreateMeshAsset();
        EggMeshFactory.Fill(mesh, EggHeight, EggWidth);
        EditorUtility.SetDirty(mesh);

        SavePrefab(mesh, material);
        AssetDatabase.SaveAssets();

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    private static Mesh LoadOrCreateMeshAsset()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);

        if (mesh != null)
        {
            return mesh;
        }

        mesh = new Mesh { name = "EggMesh" };
        AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }

    // 임시 오브젝트를 만들어 프리팹으로 저장한 뒤 바로 지운다. (씬에 남지 않는다)
    private static void SavePrefab(Mesh mesh, Material material)
    {
        var egg = new GameObject("Egg", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));

        egg.GetComponent<MeshFilter>().sharedMesh = mesh;
        egg.GetComponent<MeshRenderer>().sharedMaterial = material;

        var meshCollider = egg.GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = true;

        PrefabUtility.SaveAsPrefabAsset(egg, PrefabPath);
        Object.DestroyImmediate(egg);
    }
}
