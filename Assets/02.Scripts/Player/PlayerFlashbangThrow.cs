using UnityEngine;
using UnityEngine.Serialization;

// 마우스 오른쪽 버튼을 누르고 있으면 힘을 모으고, 떼면 바라보는 방향으로 섬광탄을 던진다. 오래 누를수록 멀리 날아간다.
// 섬광탄은 부딪히면 불이 붙어 잠시 동안 주변을 환하게 밝힌다. (폭발·피해 없음, 동작은 FlashbangProjectile이 맡는다)
public class PlayerFlashbangThrow : MonoBehaviour
{
    [FormerlySerializedAs("_eggPrefab")]
    [SerializeField] private FlashbangProjectile _flashbangPrefab;
    [Tooltip("던지는 팔 모션을 재생할 대상. 비워 두면 자식에서 찾는다.")]
    [SerializeField] private PlayerAnimation _playerAnimation;

    [Header("Stat")]
    [SerializeField] private float _minThrowSpeed = 10f;
    [SerializeField] private float _maxThrowSpeed = 30f;
    [Tooltip("이 시간(초) 동안 누르면 최대 힘이 된다.")]
    [SerializeField] private float _maxChargeTime = 1.5f;
    [SerializeField] private float _upwardBoost = 3f;
    [SerializeField] private float _cooldown = 1f;

    private float _cooldownTimer;
    private float _chargeTimer;
    private bool _charging;
    private bool _throwPending;
    private float _pendingCharge;

    private void Awake()
    {
        if (_flashbangPrefab == null)
        {
            Debug.LogError("PlayerFlashbangThrow: 섬광탄 프리팹이 연결되지 않았습니다. (Tools > Weapons > Build Flashbang)");
            enabled = false;
            return;
        }

        if (_playerAnimation == null)
        {
            _playerAnimation = GetComponentInChildren<PlayerAnimation>();
        }
    }

    private void OnEnable()
    {
        if (_playerAnimation != null)
        {
            _playerAnimation.ThrowReleased += OnThrowReleased;
        }
    }

    private void OnDisable()
    {
        _charging = false;
        _throwPending = false;

        if (_playerAnimation != null)
        {
            _playerAnimation.ThrowReleased -= OnThrowReleased;
            _playerAnimation.SetThrowCharge(false, 0f);
        }
    }

    private void Update()
    {
        _cooldownTimer -= Time.deltaTime;

        if (!_charging)
        {
            if (Input.GetMouseButtonDown(1) && _cooldownTimer <= 0f && !_throwPending)
            {
                _charging = true;
                _chargeTimer = 0f;
            }
            return;
        }

        _chargeTimer += Time.deltaTime;
        float charge = Mathf.Clamp01(_chargeTimer / _maxChargeTime);

        if (_playerAnimation != null)
        {
            _playerAnimation.SetThrowCharge(true, charge);
        }

        if (Input.GetMouseButtonUp(1))
        {
            _charging = false;
            _cooldownTimer = _cooldown;

            // 던지기 모션에서 손을 떠나는 순간 섬광탄을 만든다. 모션을 쓸 수 없으면 바로 던진다.
            if (_playerAnimation != null && _playerAnimation.PlayThrowSwing())
            {
                _throwPending = true;
                _pendingCharge = charge;
            }
            else
            {
                Throw(charge);
            }
        }
    }

    private void OnThrowReleased()
    {
        if (!_throwPending)
        {
            return;
        }

        _throwPending = false;
        Throw(_pendingCharge);
    }

    private void Throw(float charge)
    {
        float throwSpeed = Mathf.Lerp(_minThrowSpeed, _maxThrowSpeed, charge);

        Transform cameraTransform = Camera.main.transform;
        Vector3 spawnPosition = cameraTransform.position + cameraTransform.forward;

        FlashbangProjectile flashbang = Instantiate(_flashbangPrefab, spawnPosition, Random.rotation);
        flashbang.Launch(cameraTransform.forward * throwSpeed + Vector3.up * _upwardBoost);
    }
}
