using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 메뉴 한 번으로 Canvas 아래에 화면 중앙 조준점(Crosshair)을 만든다.
// 씬 파일은 저장하지 않는다. 결과를 확인한 뒤 직접 저장한다. (Ctrl+Z로 되돌릴 수 있다)
public static class CrosshairBuilder
{
    private const string SpritePath = "Assets/04.Images/Crosshair.png";
    private const string CanvasName = "Canvas";
    private const string CrosshairName = "Crosshair";
    private const string UndoGroupName = "Add Crosshair";
    private const float CrosshairSize = 40f;
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    [MenuItem("Tools/UI/Add Crosshair")]
    private static void AddCrosshair()
    {
        Sprite sprite = LoadSprite();

        if (sprite == null)
        {
            Debug.LogError($"조준점 이미지를 찾지 못했습니다: {SpritePath}");
            return;
        }

        Undo.SetCurrentGroupName(UndoGroupName);
        int undoGroup = Undo.GetCurrentGroup();

        Canvas canvas = FindOrCreateCanvas();
        Image crosshair = FindOrCreateCrosshair(canvas);
        ApplyCrosshairLook(crosshair, sprite);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = crosshair.gameObject;
    }

    // UI Image에 쓰려면 Texture Type이 Sprite여야 한다.
    private static Sprite LoadSprite()
    {
        var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;

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

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    private static Canvas FindOrCreateCanvas()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas != null)
        {
            return canvas;
        }

        GameObject canvasObject = ObjectFactory.CreateGameObject(
            CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;

        return canvas;
    }

    private static Image FindOrCreateCrosshair(Canvas canvas)
    {
        Transform existing = canvas.transform.Find(CrosshairName);

        if (existing != null)
        {
            return existing.GetComponent<Image>();
        }

        GameObject crosshairObject = ObjectFactory.CreateGameObject(
            CrosshairName, typeof(RectTransform), typeof(Image));

        GameObjectUtility.SetParentAndAlign(crosshairObject, canvas.gameObject);
        return crosshairObject.GetComponent<Image>();
    }

    private static void ApplyCrosshairLook(Image crosshair, Sprite sprite)
    {
        Undo.RecordObject(crosshair, UndoGroupName);
        crosshair.sprite = sprite;
        crosshair.preserveAspect = true;
        crosshair.raycastTarget = false;

        var rect = crosshair.rectTransform;
        Undo.RecordObject(rect, UndoGroupName);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(CrosshairSize, CrosshairSize);
    }
}
