using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Canvas 왼쪽 아래에 체력 바와 스태미너 바를 만든다. 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다.
// 씬 파일은 저장하지 않는다. 결과를 확인한 뒤 직접 저장한다. (Ctrl+Z로 되돌릴 수 있다)
public static class StatusHudBuilder
{
    private const string HudName = "StatusHud";
    private const float BarWidth = 360f;
    private const float Margin = 30f;
    private static readonly Color BackColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color HealthColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    private static readonly Color StaminaColor = new Color(0.95f, 0.8f, 0.2f, 1f);

    [MenuItem("Tools/UI/Add Status Hud")]
    private static void Build()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        GameObject player = GameObject.Find("Player");

        if (canvas == null || player == null)
        {
            Debug.LogError("Canvas 또는 Player를 찾지 못했습니다.");
            return;
        }

        Undo.SetCurrentGroupName("Add Status Hud");
        int undoGroup = Undo.GetCurrentGroup();

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = Undo.AddComponent<PlayerHealth>(player);
        }

        RectTransform hud = FindOrCreate(canvas.transform, HudName);
        Stretch(hud);

        Bar healthBar = BuildBar(hud, "HealthBar", 60f, 30f, HealthColor, 22);
        Bar staminaBar = BuildBar(hud, "StaminaBar", 25f, 20f, StaminaColor, 14);

        PlayerStatusHud statusHud = hud.GetComponent<PlayerStatusHud>();
        if (statusHud == null)
        {
            statusHud = Undo.AddComponent<PlayerStatusHud>(hud.gameObject);
        }

        var serialized = new SerializedObject(statusHud);
        serialized.FindProperty("_health").objectReferenceValue = health;
        serialized.FindProperty("_move").objectReferenceValue = player.GetComponent<PlayerMove>();
        serialized.FindProperty("_healthFill").objectReferenceValue = healthBar.Fill;
        serialized.FindProperty("_healthText").objectReferenceValue = healthBar.Label;
        serialized.FindProperty("_staminaFill").objectReferenceValue = staminaBar.Fill;
        serialized.FindProperty("_staminaText").objectReferenceValue = staminaBar.Label;
        serialized.ApplyModifiedProperties();

        EditorSceneManager.MarkAllScenesDirty();
        Undo.CollapseUndoOperations(undoGroup);
    }

    private struct Bar
    {
        public RectTransform Fill;
        public Text Label;
    }

    // [배경] > [채움] + [글자] 구조의 바 하나를 만든다.
    private static Bar BuildBar(RectTransform parent, string name, float bottom, float height, Color fillColor, int fontSize)
    {
        RectTransform root = FindOrCreate(parent, name);
        Image back = GetOrAdd<Image>(root.gameObject);
        back.color = BackColor;
        back.raycastTarget = false;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = Vector2.zero;
        root.anchoredPosition = new Vector2(Margin, bottom);
        root.sizeDelta = new Vector2(BarWidth, height);

        RectTransform fill = FindOrCreate(root, "Fill");
        Stretch(fill);
        Image fillImage = GetOrAdd<Image>(fill.gameObject);
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;

        RectTransform labelRect = FindOrCreate(root, "Label");
        Stretch(labelRect);
        Text label = GetOrAdd<Text>(labelRect.gameObject);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        return new Bar { Fill = fill, Label = label };
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static RectTransform FindOrCreate(Transform parent, string name)
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
