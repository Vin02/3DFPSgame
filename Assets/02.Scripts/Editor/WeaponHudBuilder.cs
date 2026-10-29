using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 오른쪽 아래 무기 HUD(무기 이미지·총알 수·폭탄 수)를 Canvas 아래에 만들고 WeaponHud에 연결한다.
// 줌 스코프 이미지와 조준점 전환(SniperReticleSwitch)도 함께 연결한다.
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다. (Ctrl+Z로 되돌릴 수 있다)
public static class WeaponHudBuilder
{
    private const string UndoName = "Build Weapon Hud";
    private const string UiFolder = "Assets/04.Images/4_10_UI디자인레이아웃/4_10_UI디자인레이아웃/";
    private const string NormalWeaponPath = UiFolder + "05_무기 교체 UI_01/Weapon01_gun.png";
    private const string SniperWeaponPath = UiFolder + "05_무기 교체 UI_01/Weapon02_sniper.png";
    private const string BombIconPath = UiFolder + "05_무기 교쳬 UI_02/Weapon01_R_Grenade.png";
    private const string ZoomOverlayPath = UiFolder + "05_무기 교체 UI_03/IMG_SniperZoom.png";

    [MenuItem("Tools/UI/Build Weapon Hud")]
    private static void Build()
    {
        GameObject player = GameObject.Find("Player");
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (player == null || canvas == null)
        {
            Debug.LogError("Player 또는 Canvas를 찾지 못했습니다.");
            return;
        }

        var zoom = player.GetComponent<PlayerZoom>();
        var gun = player.GetComponent<PlayerGunFire>();
        var flashbang = player.GetComponent<PlayerFlashbangThrow>();
        Sprite normalWeapon = LoadSprite(NormalWeaponPath);
        Sprite sniperWeapon = LoadSprite(SniperWeaponPath);
        Sprite bombIcon = LoadSprite(BombIconPath);
        Sprite zoomOverlay = LoadSprite(ZoomOverlayPath);

        if (zoom == null || gun == null || flashbang == null || normalWeapon == null || sniperWeapon == null || bombIcon == null || zoomOverlay == null)
        {
            Debug.LogError("PlayerZoom·PlayerGunFire·PlayerFlashbangThrow 또는 UI 이미지를 찾지 못했습니다.");
            return;
        }

        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();

        RemoveOldAmmoUi(canvas.transform);
        BuildWeaponHud(canvas, zoom, gun, flashbang, normalWeapon, sniperWeapon, bombIcon);
        BuildZoomOverlay(canvas, zoom, zoomOverlay);

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("무기 HUD를 만들었습니다. 결과를 확인한 뒤 씬을 저장하세요.");
    }

    private static void BuildWeaponHud(Canvas canvas, PlayerZoom zoom, PlayerGunFire gun, PlayerFlashbangThrow flashbang,
        Sprite normalWeapon, Sprite sniperWeapon, Sprite bombIcon)
    {
        RectTransform hud = FindOrCreateRect(canvas.transform, "WeaponHud");
        Stretch(hud);

        // 오른쪽 아래에서 위로: 폭탄 → 총알 수 → 무기 이미지 (재장전 표시는 맨 위)
        Image bombImage = CreateImage(hud, "BombIcon", bombIcon);
        Place(bombImage.rectTransform, new Vector2(1f, 0f), new Vector2(-190f, 30f), new Vector2(40f, 56f));
        Text bombText = CreateText(hud, "BombCounter", 38, TextAnchor.LowerRight, Color.white);
        Place(bombText.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(140f, 56f));

        Text ammoText = CreateText(hud, "AmmoCounter", 48, TextAnchor.LowerRight, Color.white);
        Place(ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 95f), new Vector2(400f, 70f));

        Image weaponImage = CreateImage(hud, "WeaponIcon", normalWeapon);
        Place(weaponImage.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 170f), new Vector2(260f, 86f));

        Text grenadeText = CreateText(hud, "GrenadeText", 26, TextAnchor.LowerRight, Color.white);
        Place(grenadeText.rectTransform, new Vector2(1f, 0f), new Vector2(-245f, 36f), new Vector2(320f, 44f));

        Text reloadText = CreateText(hud, "ReloadText", 22, TextAnchor.LowerRight, new Color(1f, 0.8f, 0.3f));
        Place(reloadText.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 262f), new Vector2(400f, 34f));
        reloadText.text = "RELOADING...";

        WeaponHud weaponHud = GetOrAddComponent<WeaponHud>(hud.gameObject);
        var serialized = new SerializedObject(weaponHud);
        serialized.FindProperty("_zoom").objectReferenceValue = zoom;
        serialized.FindProperty("_gun").objectReferenceValue = gun;
        serialized.FindProperty("_flashbang").objectReferenceValue = flashbang;
        serialized.FindProperty("_weaponIcon").objectReferenceValue = weaponImage;
        serialized.FindProperty("_ammoText").objectReferenceValue = ammoText;
        serialized.FindProperty("_bombText").objectReferenceValue = bombText;
        serialized.FindProperty("_reloadText").objectReferenceValue = reloadText;
        serialized.FindProperty("_grenadeText").objectReferenceValue = grenadeText;
        serialized.FindProperty("_normalWeaponSprite").objectReferenceValue = normalWeapon;
        serialized.FindProperty("_sniperWeaponSprite").objectReferenceValue = sniperWeapon;
        serialized.ApplyModifiedProperties();
    }

    // 줌 스코프 이미지는 다른 HUD 뒤(맨 아래)에 깔리게 한다. 줌 중에만 켜지므로 처음엔 꺼 둔다.
    private static void BuildZoomOverlay(Canvas canvas, PlayerZoom zoom, Sprite overlaySprite)
    {
        RectTransform overlay = FindOrCreateRect(canvas.transform, "SniperZoomOverlay");
        Image image = GetOrAddComponent<Image>(overlay.gameObject);
        image.sprite = overlaySprite;
        image.raycastTarget = false;
        image.preserveAspect = false;
        Stretch(overlay);
        overlay.SetAsFirstSibling();
        overlay.gameObject.SetActive(false);

        Transform crosshair = canvas.transform.Find("Crosshair");
        if (crosshair == null)
        {
            Debug.LogWarning("Canvas에 Crosshair가 없습니다. Tools > UI > Add Crosshair를 먼저 실행하세요.");
            return;
        }

        // 저격 전용 빨간 십자선은 더 쓰지 않는다. (줌 안 할 때는 기본 조준점과 같게)
        Transform oldReticle = canvas.transform.Find("SniperReticle");
        if (oldReticle != null)
        {
            Undo.DestroyObjectImmediate(oldReticle.gameObject);
        }

        var reticleSwitch = GetOrAddComponent<SniperReticleSwitch>(canvas.gameObject);
        var serialized = new SerializedObject(reticleSwitch);
        serialized.FindProperty("_zoom").objectReferenceValue = zoom;
        serialized.FindProperty("_normalCrosshair").objectReferenceValue = crosshair.gameObject;
        serialized.FindProperty("_zoomOverlay").objectReferenceValue = overlay.gameObject;
        serialized.ApplyModifiedProperties();
    }

    // 예전 CombatHud 안에 있던 탄약·재장전 글자는 WeaponHud로 옮겼으므로 지운다.
    private static void RemoveOldAmmoUi(Transform canvas)
    {
        Transform combatHud = canvas.Find("CombatHud");
        if (combatHud == null)
        {
            return;
        }

        foreach (string name in new[] { "AmmoCounter", "ReloadText" })
        {
            Transform old = combatHud.Find(name);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }
        }
    }

    private static Sprite LoadSprite(string path)
    {
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
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite)
    {
        RectTransform rect = FindOrCreateRect(parent, name);
        Image image = GetOrAddComponent<Image>(rect.gameObject);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment, Color color)
    {
        RectTransform rect = FindOrCreateRect(parent, name);
        Text text = GetOrAddComponent<Text>(rect.gameObject);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;

        Shadow shadow = GetOrAddComponent<Shadow>(rect.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static RectTransform FindOrCreateRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return (RectTransform)existing;
        }

        GameObject created = ObjectFactory.CreateGameObject(name, typeof(RectTransform));
        GameObjectUtility.SetParentAndAlign(created, parent.gameObject);
        return (RectTransform)created.transform;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }
}
