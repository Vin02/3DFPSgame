using System;
using UnityEngine;

// 1번 키는 일반 모드, 2번 키는 저격 모드. 저격 모드에서 마우스 오른쪽 버튼을 누르고 있으면 FOV를 줄여 줌(조준)한다.
// 일반 모드의 오른쪽 버튼(섬광탄 던지기)은 PlayerFlashbangThrow가 Mode를 보고 막는다.
// 모드나 줌 상태가 바뀌면 이벤트로 알린다. (UI·총 등이 구독)
public class PlayerZoom : MonoBehaviour
{
    [SerializeField] private Camera _camera;
    [SerializeField] private KeyCode _normalKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode _sniperKey = KeyCode.Alpha2;

    [Header("Zoom")]
    [Tooltip("줌 중일 때의 FOV. 작을수록 더 확대된다.")]
    [SerializeField] private float _zoomFieldOfView = 42f;
    [SerializeField] private float _zoomSpeed = 12f;

    private WeaponMode _mode = WeaponMode.Normal;
    private bool _isZooming;
    private float _defaultFieldOfView;

    public event Action<WeaponMode> ModeChanged;
    public event Action<bool> ZoomingChanged;

    public WeaponMode Mode => _mode;
    public bool IsZooming => _isZooming;

    private void Awake()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
        }

        if (_camera == null)
        {
            Debug.LogError("PlayerZoom: 카메라를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        _defaultFieldOfView = _camera.fieldOfView;
    }

    private void OnDisable()
    {
        SetZooming(false);

        if (_camera != null)
        {
            _camera.fieldOfView = _defaultFieldOfView;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(_normalKey))
        {
            SetMode(WeaponMode.Normal);
        }
        else if (Input.GetKeyDown(_sniperKey))
        {
            SetMode(WeaponMode.Sniper);
        }

        SetZooming(_mode == WeaponMode.Sniper && Input.GetMouseButton(1));

        float targetFieldOfView = _isZooming ? _zoomFieldOfView : _defaultFieldOfView;
        float blend = 1f - Mathf.Exp(-_zoomSpeed * Time.deltaTime);
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFieldOfView, blend);
    }

    private void SetMode(WeaponMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        ModeChanged?.Invoke(_mode);
    }

    private void SetZooming(bool zooming)
    {
        if (_isZooming == zooming)
        {
            return;
        }

        _isZooming = zooming;
        ZoomingChanged?.Invoke(_isZooming);
    }
}
