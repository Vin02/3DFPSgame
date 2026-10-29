using UnityEngine;

// 줌 상태에 맞춰 조준 UI를 바꾼다. (PlayerZoom의 ZoomingChanged 이벤트를 구독)
//  - 줌 안 할 때: 모드와 상관없이 기본 조준점
//  - 저격 모드에서 줌 중: 화면 전체를 덮는 스코프 이미지 (십자선은 이미지에 들어 있으므로 기본 조준점은 숨긴다)
public class SniperReticleSwitch : MonoBehaviour
{
    [SerializeField] private PlayerZoom _zoom;
    [SerializeField] private GameObject _normalCrosshair;
    [SerializeField] private GameObject _zoomOverlay;

    private void Awake()
    {
        if (_zoom == null || _normalCrosshair == null || _zoomOverlay == null)
        {
            Debug.LogError("SniperReticleSwitch: 참조가 비어 있습니다.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        _zoom.ZoomingChanged += OnZoomingChanged;
        OnZoomingChanged(_zoom.IsZooming);
    }

    private void OnDisable()
    {
        _zoom.ZoomingChanged -= OnZoomingChanged;
    }

    private void OnZoomingChanged(bool zooming)
    {
        _zoomOverlay.SetActive(zooming);
        _normalCrosshair.SetActive(!zooming);
    }
}
