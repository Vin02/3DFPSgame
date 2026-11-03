using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 평소엔 완전히 투명하고, 마우스를 올리면 하이라이트 색, 누르고 있으면 더 진한 색을 Image에 칠한다.
// 영상·배경 이미지에 이미 그려진 버튼 위에 겹쳐 호버 효과만 낼 때 쓴다. (Button의 Transition은 None으로 둔다)
[RequireComponent(typeof(Image))]
public class HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Color _hoverColor = new Color(0.55f, 1f, 0.65f, 0.22f);
    [SerializeField] private Color _pressedColor = new Color(0.55f, 1f, 0.65f, 0.4f);

    private static readonly Color Hidden = new Color(1f, 1f, 1f, 0f);

    private Image _image;
    private bool _hovered;
    private bool _pressed;

    private void Awake()
    {
        _image = GetComponent<Image>();
        Refresh();
    }

    private void OnDisable()
    {
        _hovered = false;
        _pressed = false;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        Refresh();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        Refresh();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        Refresh();
    }

    private void Refresh()
    {
        if (_image == null)
        {
            return;
        }

        _image.color = _pressed && _hovered ? _pressedColor : _hovered ? _hoverColor : Hidden;
    }
}
