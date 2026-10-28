using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 화면 우측 상단 원형 미니맵(카메라, RenderTexture, UI, 플레이어·적 아이콘)을 만든다. 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다.
// 씬 파일은 저장하지 않는다. 결과를 확인한 뒤 직접 저장한다. (Ctrl+Z로 되돌릴 수 있다)
public static class MinimapBuilder
{
    private const string ResourceDir = "Assets/04.Images/Minimap_Resource/";
    private const string RenderTexturePath = ResourceDir + "MinimapRT.renderTexture";
    private const string LayerName = "Minimap";
    private const int LayerIndex = 7;
    private const string CameraName = "MinimapCamera";
    private const string RootName = "Minimap";
    private const string IconName = "MinimapIcon";
    private const float CameraHeight = 20f;
    private const float OrthoSize = 14f;
    private const float PlayerIconSize = 3f;
    private const float EnemyIconSize = 3f;
    private const float UiSize = 240f;
    private const float UiMargin = 20f;

    [MenuItem("Tools/UI/Add Minimap")]
    private static void Build()
    {
        Undo.SetCurrentGroupName("Add Minimap");
        int undoGroup = Undo.GetCurrentGroup();

        EnsureLayer();
        Sprite playerIcon = LoadSprite("MinimapIcon_Player.png");
        Sprite enemyIcon = LoadSprite("MinimapIcon_Enemy01.png");
        Sprite mask = LoadSprite("MinimapMask01.png");
        Sprite frame = LoadSprite("MinimapRound01.png");
        RenderTexture renderTexture = FindOrCreateRenderTexture();

        GameObject player = GameObject.Find("Player");
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (player == null || canvas == null || playerIcon == null || enemyIcon == null || mask == null || frame == null)
        {
            Debug.LogError("플레이어, Canvas 또는 미니맵 이미지를 찾지 못했습니다.");
            return;
        }

        Camera minimapCamera = FindOrCreateCamera(player.transform, renderTexture);
        HideLayerFromMainCameras(minimapCamera);

        AddIcon(player.transform, playerIcon, PlayerIconSize, minimapCamera.transform);
        foreach (Zombie zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.InstanceID))
        {
            AddIcon(zombie.transform, enemyIcon, EnemyIconSize, minimapCamera.transform);
        }

        BuildUi(canvas, renderTexture, mask, frame);

        EditorSceneManager.MarkAllScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);
    }

    // 비어 있는 레이어 슬롯에 Minimap 레이어를 등록한다.
    private static void EnsureLayer()
    {
        if (LayerMask.NameToLayer(LayerName) >= 0)
        {
            return;
        }

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layer = tagManager.FindProperty("layers").GetArrayElementAtIndex(LayerIndex);

        if (!string.IsNullOrEmpty(layer.stringValue))
        {
            Debug.LogError($"레이어 {LayerIndex}번이 이미 사용 중입니다: {layer.stringValue}");
            return;
        }

        layer.stringValue = LayerName;
        tagManager.ApplyModifiedProperties();
    }

    // UI와 SpriteRenderer에 쓰려면 Texture Type이 Sprite여야 한다.
    private static Sprite LoadSprite(string fileName)
    {
        string path = ResourceDir + fileName;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
        {
            return null;
        }

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static RenderTexture FindOrCreateRenderTexture()
    {
        var renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);

        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(512, 512, 16) { name = "MinimapRT" };
            AssetDatabase.CreateAsset(renderTexture, RenderTexturePath);
        }

        return renderTexture;
    }

    private static Camera FindOrCreateCamera(Transform player, RenderTexture renderTexture)
    {
        GameObject cameraObject = GameObject.Find(CameraName);

        if (cameraObject == null)
        {
            cameraObject = ObjectFactory.CreateGameObject(CameraName, typeof(Camera));
        }

        Camera camera = cameraObject.GetComponent<Camera>();
        Undo.RecordObject(camera, "Setup minimap camera");
        camera.orthographic = true;
        camera.orthographicSize = OrthoSize;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.1f, 0.12f, 0.1f, 1f);
        camera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
        camera.targetTexture = renderTexture;
        camera.depth = -10f;

        MinimapCaemra follow = cameraObject.GetComponent<MinimapCaemra>();
        if (follow == null)
        {
            follow = Undo.AddComponent<MinimapCaemra>(cameraObject);
        }

        var serialized = new SerializedObject(follow);
        serialized.FindProperty("_target").objectReferenceValue = player;
        serialized.FindProperty("_height").floatValue = CameraHeight;
        serialized.ApplyModifiedProperties();
        return camera;
    }

    // 아이콘은 Minimap 레이어에만 두고, 메인 카메라에서는 보이지 않게 한다.
    private static void HideLayerFromMainCameras(Camera minimapCamera)
    {
        int bit = 1 << LayerMask.NameToLayer(LayerName);

        foreach (Camera camera in Camera.allCameras)
        {
            if (camera != minimapCamera && camera.targetTexture == null)
            {
                Undo.RecordObject(camera, "Hide minimap layer");
                camera.cullingMask &= ~bit;
            }
        }
    }

    private static void AddIcon(Transform owner, Sprite sprite, float worldSize, Transform minimapCamera)
    {
        Transform existing = owner.Find(IconName);
        GameObject iconObject = existing != null
            ? existing.gameObject
            : ObjectFactory.CreateGameObject(IconName, typeof(SpriteRenderer));

        if (existing == null)
        {
            iconObject.transform.SetParent(owner, false);
        }

        iconObject.layer = LayerMask.NameToLayer(LayerName);

        // 소유자 스케일과 상관없이 월드 크기를 맞춘다.
        Vector3 ownerScale = owner.lossyScale;
        float spriteSize = sprite.bounds.size.x;
        iconObject.transform.localScale = new Vector3(
            worldSize / (spriteSize * ownerScale.x),
            worldSize / (spriteSize * ownerScale.y),
            worldSize / (spriteSize * ownerScale.z));
        iconObject.transform.position = owner.position + Vector3.up * (CameraHeight * 0.5f);
        iconObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        SpriteRenderer spriteRenderer = iconObject.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = 10;

        MinimapIcon icon = iconObject.GetComponent<MinimapIcon>();
        if (icon == null)
        {
            icon = Undo.AddComponent<MinimapIcon>(iconObject);
        }

        var serialized = new SerializedObject(icon);
        serialized.FindProperty("_minimapCamera").objectReferenceValue = minimapCamera;
        serialized.ApplyModifiedProperties();
    }

    // Canvas 우측 상단에 [마스크(원) > 미니맵 영상] + 테두리를 만든다.
    private static void BuildUi(Canvas canvas, RenderTexture renderTexture, Sprite maskSprite, Sprite frameSprite)
    {
        RectTransform root = FindOrCreateUi(canvas.transform, RootName, typeof(Image), typeof(Mask));
        PlaceTopRight(root);

        Image maskImage = root.GetComponent<Image>();
        maskImage.sprite = maskSprite;
        maskImage.raycastTarget = false;
        root.GetComponent<Mask>().showMaskGraphic = false;

        RectTransform view = FindOrCreateUi(root, "View", typeof(RawImage));
        Stretch(view);
        RawImage rawImage = view.GetComponent<RawImage>();
        rawImage.texture = renderTexture;
        rawImage.raycastTarget = false;

        RectTransform frame = FindOrCreateUi(root, "Frame", typeof(Image));
        Stretch(frame);
        Image frameImage = frame.GetComponent<Image>();
        frameImage.sprite = frameSprite;
        frameImage.raycastTarget = false;
    }

    private static RectTransform FindOrCreateUi(Transform parent, string name, params System.Type[] components)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
        {
            return (RectTransform)existing;
        }

        GameObject created = ObjectFactory.CreateGameObject(name, typeof(RectTransform));
        foreach (System.Type component in components)
        {
            created.AddComponent(component);
        }

        GameObjectUtility.SetParentAndAlign(created, parent.gameObject);
        return (RectTransform)created.transform;
    }

    private static void PlaceTopRight(RectTransform rect)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-UiMargin, -UiMargin);
        rect.sizeDelta = new Vector2(UiSize, UiSize);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
