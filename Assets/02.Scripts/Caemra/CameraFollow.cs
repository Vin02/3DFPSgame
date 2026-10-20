using UnityEngine;

// 대상을 따라가며 현재 CameraView에 맞춰 카메라의 위치와 회전을 결정한다.
// 시점이 바뀌면 일정 시간 동안 부드럽게 이동하고, 이후에는 지연 없이 따라간다.
public class CameraFollow : MonoBehaviour
{
    private const float OffsetArrivalThreshold = 0.01f;
    private const float AngleArrivalThreshold = 0.5f;

    [SerializeField] private Transform _target;
    [SerializeField] private CameraRotate _cameraRotate;
    [SerializeField] private float _blendSpeed = 10f;

    private CameraView _view;
    private Vector3 _currentOffset;
    private Quaternion _currentRotation;
    private bool _isBlending;

    private void Awake()
    {
        if (_cameraRotate == null)
        {
            _cameraRotate = GetComponent<CameraRotate>();
        }

        if (_target == null || _cameraRotate == null)
        {
            GameLogger.LogWarning("카메라 대상 또는 CameraRotate가 없습니다.");
            enabled = false;
            return;
        }

        _currentRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (_view == null)
        {
            return;
        }

        Quaternion desiredRotation = GetDesiredRotation();
        float blendFactor = _isBlending ? 1f - Mathf.Exp(-_blendSpeed * Time.deltaTime) : 1f;

        _currentOffset = Vector3.Lerp(_currentOffset, _view.Offset, blendFactor);
        _currentRotation = Quaternion.Slerp(_currentRotation, desiredRotation, blendFactor);

        transform.SetPositionAndRotation(
            _target.position + _currentRotation * _currentOffset,
            _currentRotation);

        if (_isBlending)
        {
            _isBlending = !HasArrived(desiredRotation);
        }
    }

    public void SetView(CameraView view)
    {
        _view = view;
        _isBlending = true;
    }

    private Quaternion GetDesiredRotation()
    {
        return _view.IsFreeLook ? _cameraRotate.Rotation : _view.FixedRotation;
    }

    private bool HasArrived(Quaternion desiredRotation)
    {
        bool isOffsetArrived = (_view.Offset - _currentOffset).sqrMagnitude
            < OffsetArrivalThreshold * OffsetArrivalThreshold;
        bool isRotationArrived = Quaternion.Angle(_currentRotation, desiredRotation)
            < AngleArrivalThreshold;

        return isOffsetArrived && isRotationArrived;
    }
}
