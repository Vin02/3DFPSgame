using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 제목 + 세로 버튼 목록으로 이루어진 중앙 메뉴 창(일시정지, 게임오버 공용)을 코드로 만든다.
// GameManager가 소유하며, 씬 파일은 건드리지 않는다.
public class MenuPanel
{
    private const float ButtonHeight = 80f;
    private const float ButtonSpacing = 100f;
    private const float TopArea = 190f;
    private const float BottomPadding = 50f;

    private static readonly Color PanelColor = new Color(0.13f, 0.15f, 0.2f, 0.95f);
    private static readonly Color ButtonColor = Color.white;
    private static readonly Color ButtonPressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);

    private readonly GameObject _root;
    private readonly GameObject _firstButton;

    public readonly struct Item
    {
        public readonly string Label;
        public readonly Action OnClick;

        public Item(string label, Action onClick)
        {
            Label = label;
            OnClick = onClick;
        }
    }

    public MenuPanel(Transform parent, string name, string title, Color dimColor, Color titleColor, int sortingOrder, params Item[] items)
    {
        // 시간이 멈춰도 보이도록 최상단 Canvas로 만든다.
        _root = new GameObject(name, typeof(RectTransform));
        _root.transform.SetParent(parent, false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();

        Image dim = CreateImage(_root.transform, "Dim", dimColor);
        Stretch(dim.rectTransform);

        float height = TopArea + items.Length * ButtonSpacing + BottomPadding - (ButtonSpacing - ButtonHeight);
        Image panel = CreateImage(_root.transform, "Panel", PanelColor);
        Center(panel.rectTransform, new Vector2(520f, height));

        Text titleText = CreateText(panel.transform, "Title", title, 64, titleColor);
        Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(480f, 100f));

        for (int i = 0; i < items.Length; i++)
        {
            GameObject button = CreateButton(panel.transform, "Button" + i, items[i], -(TopArea + ButtonHeight * 0.5f) - i * ButtonSpacing);
            if (i == 0)
            {
                _firstButton = button;
            }
        }

        _root.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        if (_root == null || _root.activeSelf == visible)
        {
            return;
        }

        _root.SetActive(visible);
        if (visible)
        {
            EnsureEventSystem();
        }

        if (visible && _firstButton != null && EventSystem.current != null)
        {
            // 키보드/패드로도 바로 누를 수 있게 첫 버튼을 선택한다.
            EventSystem.current.SetSelectedGameObject(_firstButton);
        }
    }

    // 씬에 EventSystem이 없을 때만 만든다. 일부러 현재 씬 루트에 둬서, 씬이 바뀌면 사라지고
    // 다음 씬이 가진 EventSystem과 겹치지 않게 한다.
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("[EventSystem]", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static GameObject CreateButton(Transform parent, string name, Item item, float y)
    {
        Image image = CreateImage(parent, name, ButtonColor);
        Anchor(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(440f, ButtonHeight));

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = ButtonPressedColor;
        button.colors = colors;
        Action onClick = item.OnClick;
        button.onClick.AddListener(() => onClick());

        Text text = CreateText(image.transform, "Label", item.Label, 36, new Color(0.1f, 0.1f, 0.1f));
        Stretch(text.rectTransform);
        return image.gameObject;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(Transform parent, string name, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
