using System;
using UnityEngine;

// 무기 모드는 Player가 관리한다(1번 일반, 2번 저격, 3번 근접). 저격 모드에서 마우스 오른쪽 버튼을 누르고 있으면 FOV를 줄여 줌(조준)한다.
// 일반 모드의 오른쪽 버튼(섬광탄 던지기)은 PlayerFlashbangThrow가 Mode를 보고 막는다.
// 모드나 줌 상태가 바뀌면 이벤트로 알린다. (UI·총 등이 구독)
public class PlayerZoom : MonoBehaviour
{
    [SerializeField] private Camera _camera;

    [Header("Zoom")]
    [Tooltip("줌 중일 때의 FOV. 작을수록 더 확대된다.")]
    [SerializeField] private float _zoomFieldOfView = 42f;
    [SerializeField] private float _zoomSpeed = 12f;

    private Player _player;
    private bool _isZooming;
    private float _defaultFieldOfView;

    public event Action<WeaponMode> ModeChanged;
    public event Action<bool> ZoomingChanged;

    public WeaponMode Mode => _player.Mode;
    public bool IsZooming => _isZooming;

    private void Awake()
    {
        // 씬에 Player가 아직 붙어 있지 않으면 같은 오브젝트에 자동으로 붙인다.
        if (!TryGetComponent(out _player))
        {
            _player = gameObject.AddComponent<Player>();
        }

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

    private void OnEnable()
    {
        _player.ModeChanged += OnPlayerModeChanged;
    }

    private void OnDisable()
    {
        _player.ModeChanged -= OnPlayerModeChanged;
        SetZooming(false);

        if (_camera != null)
        {
            _camera.fieldOfView = _defaultFieldOfView;
        }
    }

    private void Update()
    {
        if (GameManager.IsPaused)
        {
            return;
        }

        SetZooming(_player.Mode == WeaponMode.Sniper && Input.GetMouseButton(1));

        float targetFieldOfView = _isZooming ? _zoomFieldOfView : _defaultFieldOfView;
        float blend = 1f - Mathf.Exp(-_zoomSpeed * Time.deltaTime);
        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFieldOfView, blend);
    }

    private void OnPlayerModeChanged(WeaponMode mode)
    {
        ModeChanged?.Invoke(mode);
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
