using System;
using UnityEngine;

// 마우스 오른쪽 버튼을 누르고 있으면 힘을 모으고, 떼면 바라보는 방향으로 수류탄(행성)을 던진다. 오래 누를수록 멀리 날아간다.
// 수류탄은 정해진 순서(태양 → 수성 → … → 해왕성)로 한 개씩 나가고, 다 던져야만 장전 키로 다시 채울 수 있다.
// 남은 개수와 장전 상태가 바뀌면 이벤트로 알린다. (UI가 구독)
// 던져진 수류탄의 동작(불이 붙어 밝히고 주변 좀비에게 피해)은 FlashbangProjectile이 맡는다.
public class PlayerFlashbangThrow : MonoBehaviour
{
    [Tooltip("던지는 순서대로. 개수는 Max Bombs와 같아야 한다. (Tools > Weapons > Build Planet Grenades)")]
    [SerializeField] private FlashbangProjectile[] _grenadePrefabs;
    [Tooltip("던지는 팔 모션을 재생할 대상. 비워 두면 자식에서 찾는다.")]
    [SerializeField] private PlayerAnimation _playerAnimation;

    [Header("Stat")]
    [SerializeField] private float _minThrowSpeed = 10f;
    [SerializeField] private float _maxThrowSpeed = 30f;
    [Tooltip("이 시간(초) 동안 누르면 최대 힘이 된다.")]
    [SerializeField] private float _maxChargeTime = 1.5f;
    [SerializeField] private float _upwardBoost = 3f;
    [SerializeField] private float _cooldown = 1f;
    [SerializeField] private int _maxBombs = 10;

    [Header("장전")]
    [Tooltip("다 던진 뒤에만 눌러서 장전한다.")]
    [SerializeField] private KeyCode _reloadKey = KeyCode.G;
    [SerializeField] private float _reloadTime = 2.5f;

    private float _cooldownTimer;
    private float _chargeTimer;
    private bool _charging;
    private bool _throwPending;
    private float _pendingCharge;
    private int _pendingIndex;
    private float _reloadTimer;
    private PlayerZoom _zoom;
    private int _bombs;

    // (남은 개수, 최대 개수)
    public event Action<int, int> BombsChanged;
    public event Action<bool> ReloadingChanged;

    public int Bombs => _bombs;
    public int MaxBombs => _maxBombs;
    public bool IsReloading => _reloadTimer > 0f;

    // 다음에 던질 수류탄 이름. 다 던졌으면 빈 문자열
    public string NextName => _bombs > 0 ? _grenadePrefabs[_maxBombs - _bombs].DisplayName : string.Empty;

    private void Awake()
    {
        if (_grenadePrefabs == null || _grenadePrefabs.Length != _maxBombs || Array.IndexOf(_grenadePrefabs, null) >= 0)
        {
            Debug.LogError("PlayerFlashbangThrow: 수류탄 프리팹이 Max Bombs 개수만큼 연결되지 않았습니다. (Tools > Weapons > Build Planet Grenades)");
            enabled = false;
            return;
        }

        if (_playerAnimation == null)
        {
            _playerAnimation = GetComponentInChildren<PlayerAnimation>();
        }

        _zoom = GetComponent<PlayerZoom>();
        _bombs = _maxBombs;
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
        UpdateReload();

        // 저격 모드에서는 오른쪽 버튼이 줌이므로 던지지 않는다. 모으던 중이면 취소한다.
        if (_zoom != null && _zoom.Mode == WeaponMode.Sniper)
        {
            if (_charging)
            {
                _charging = false;
                if (_playerAnimation != null)
                {
                    _playerAnimation.SetThrowCharge(false, 0f);
                }
            }
            return;
        }

        if (!_charging)
        {
            if (Input.GetMouseButtonDown(1) && _cooldownTimer <= 0f && !_throwPending && _bombs > 0)
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
            int index = _maxBombs - _bombs; // 이번에 던질 수류탄의 순서
            _bombs--;
            BombsChanged?.Invoke(_bombs, _maxBombs);

            // 던지기 모션에서 손을 떠나는 순간 수류탄을 만든다. 모션을 쓸 수 없으면 바로 던진다.
            if (_playerAnimation != null && _playerAnimation.PlayThrowSwing())
            {
                _throwPending = true;
                _pendingCharge = charge;
                _pendingIndex = index;
            }
            else
            {
                Throw(charge, index);
            }
        }
    }

    // 다 던졌을 때만 장전할 수 있다. 장전이 끝나면 처음 순서(태양)부터 다시 10개.
    private void UpdateReload()
    {
        if (IsReloading)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                _reloadTimer = 0f;
                _bombs = _maxBombs;
                BombsChanged?.Invoke(_bombs, _maxBombs);
                ReloadingChanged?.Invoke(false);
            }

            return;
        }

        if (_bombs == 0 && !_throwPending && Input.GetKeyDown(_reloadKey))
        {
            _reloadTimer = _reloadTime;
            ReloadingChanged?.Invoke(true);
        }
    }

    private void OnThrowReleased()
    {
        if (!_throwPending)
        {
            return;
        }

        _throwPending = false;
        Throw(_pendingCharge, _pendingIndex);
    }

    private void Throw(float charge, int index)
    {
        float throwSpeed = Mathf.Lerp(_minThrowSpeed, _maxThrowSpeed, charge);

        Transform cameraTransform = Camera.main.transform;
        Vector3 spawnPosition = cameraTransform.position + cameraTransform.forward;

        FlashbangProjectile grenade = Instantiate(_grenadePrefabs[index], spawnPosition, UnityEngine.Random.rotation);
        grenade.Launch(cameraTransform.forward * throwSpeed + Vector3.up * _upwardBoost);
    }
}
