using System;
using UnityEngine;
using UnityEngine.Serialization;

//마우스 왼쪽 버튼 누르면 바라보는 방향으로 총알(Bullet 프리팹)을 발사
// 탄창 하나에 정해진 수만큼 쏠 수 있고, R 키(또는 빈 탄창으로 쏘려 할 때) 재장전한다. 탄창 수는 무제한이다.
// 일반 모드와 저격 모드는 탄창·재장전 시간·데미지·연사 속도가 따로이고, 모드별로 남은 총알을 따로 기억한다.
// 총알 수나 재장전 상태가 바뀌면 이벤트로 알린다. (UI가 구독)
public class PlayerGunFire : MonoBehaviour
{
    [Header("Magazine")]
    [SerializeField] private int _magazineSize = 30;
    [SerializeField] private float _reloadTime = 1.8f;
    [SerializeField] private KeyCode _reloadKey = KeyCode.R;

    [Header("Effects (풀)")]
    [SerializeField] private EffectPool _hitEffectPool;
    [SerializeField] private EffectPool _bloodEffectPool;
    [Tooltip("총알 프리팹(Bullet)을 담은 풀")]
    [SerializeField] private EffectPool _bulletPool;
    [Header("검기 (총알 대신)")]
    [Tooltip("초록 검기 하나당 풀 하나. 비어 있으면 위의 총알을 쏜다. (Tools > Weapons > Build Sword Waves)")]
    [SerializeField] private EffectPool[] _swordWavePools;
    [Tooltip("한 번 쏠 때 나가는 검기 수의 범위. 이 범위에서 개수를 뽑고, 그만큼의 종류를 겹치지 않게 고른다.")]
    [SerializeField] private int _waveCountMin = 1;
    [SerializeField] private int _waveCountMax = 4;
    [Tooltip("검기가 둘 이상일 때 서로 겹쳐 보이지 않도록 벌리는 최대 각도")]
    [SerializeField] private float _waveSpreadAngle = 4f;
    [Header("Bullet")]
    [Tooltip("총구 위치. 총을 든 손 본의 자식 오브젝트")]
    [SerializeField] private Transform _muzzle;
    [Tooltip("총구가 없을 때 쓰는 카메라 기준 시작 위치 (오른쪽, 위, 앞)")]
    [FormerlySerializedAs("_tracerStartOffset")]
    [SerializeField] private Vector3 _muzzleFallbackOffset = new Vector3(0.25f, -0.2f, 0.7f);
    [Header("총구 화염")]
    [Tooltip("쏠 때마다 이 중 하나가 총구에 나온다.")]
    [SerializeField] private GameObject[] _muzzleFlashPrefabs;
    [SerializeField] private float _muzzleFlashTime = 0.06f;
    [SerializeField] private float _sniperFlashScale = 1.8f;
    [Tooltip("조준과 총알이 맞힐 수 있는 레이어 (플레이어 자신은 빼야 한다)")]
    [SerializeField] private LayerMask _hitMask = ~0;
    [Header("Stat")]
    [SerializeField] private float _range = 100f;
    [SerializeField] private float _damage = 80f;
    [SerializeField] private float _fireInterval = 0.1f;
    [Header("Sniper Stat (저격 모드)")]
    [SerializeField] private int _sniperMagazineSize = 5;
    [SerializeField] private float _sniperReloadTime = 2.5f;
    [SerializeField] private float _sniperDamage = 200f;
    [SerializeField] private float _sniperFireInterval = 1.2f;

    private PlayerZoom _zoom;
    private int _normalAmmo;
    private int _sniperAmmo;
    private float _reloadTimer;
    private float _fireTimer;
    private int[] _waveOrder;

    // (현재 총알 수, 탄창 크기)
    public event Action<int, int> AmmoChanged;
    public event Action<bool> ReloadingChanged;

    private bool IsSniper => _zoom != null && _zoom.Mode == WeaponMode.Sniper;

    public int Ammo => IsSniper ? _sniperAmmo : _normalAmmo;
    public int MagazineSize => IsSniper ? _sniperMagazineSize : _magazineSize;
    public bool IsReloading => _reloadTimer > 0f;
    private float ReloadTime => IsSniper ? _sniperReloadTime : _reloadTime;
    // 0(시작) ~ 1(완료). 재장전 중이 아니면 0.
    public float ReloadProgress => IsReloading ? 1f - _reloadTimer / ReloadTime : 0f;

    private void Awake()
    {
        _normalAmmo = _magazineSize;
        _sniperAmmo = _sniperMagazineSize;
        _zoom = GetComponent<PlayerZoom>();
    }

    private void OnEnable()
    {
        if (_zoom != null)
        {
            _zoom.ModeChanged += OnModeChanged;
        }
    }

    private void OnDisable()
    {
        if (_zoom != null)
        {
            _zoom.ModeChanged -= OnModeChanged;
        }
    }

    private void Update()
    {
        _fireTimer -= Time.deltaTime;

        if (IsReloading)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                _reloadTimer = 0f;
                SetAmmo(MagazineSize);
                ReloadingChanged?.Invoke(false);
            }
            return;
        }

        if (Input.GetKeyDown(_reloadKey) && Ammo < MagazineSize)
        {
            StartReload();
            return;
        }

        // 1. 마우스 왼쪽 버튼 입력 받기
        if (Input.GetButton("Fire1") && _fireTimer <= 0f)
        {
            if (Ammo <= 0)
            {
                StartReload();
                return;
            }

            _fireTimer = IsSniper ? _sniperFireInterval : _fireInterval;
            SetAmmo(Ammo - 1);
            Fire();
        }
    }

    // 모드를 바꾸면 재장전은 취소하고, 그 모드의 남은 총알 수를 다시 알린다.
    private void OnModeChanged(WeaponMode mode)
    {
        if (IsReloading)
        {
            _reloadTimer = 0f;
            ReloadingChanged?.Invoke(false);
        }

        AmmoChanged?.Invoke(Ammo, MagazineSize);
    }

    private void StartReload()
    {
        _reloadTimer = ReloadTime;
        ReloadingChanged?.Invoke(true);
    }

    private void SetAmmo(int ammo)
    {
        if (IsSniper)
        {
            _sniperAmmo = ammo;
        }
        else
        {
            _normalAmmo = ammo;
        }

        AmmoChanged?.Invoke(Ammo, MagazineSize);
    }

    // 총구에 화염을 잠깐 띄운다. 프리팹은 스스로 사라지지만 너무 오래 남으므로 더 일찍 지운다.
    private void ShowMuzzleFlash(Vector3 origin, Vector3 direction)
    {
        if (_muzzleFlashPrefabs == null || _muzzleFlashPrefabs.Length == 0)
        {
            return;
        }

        GameObject prefab = _muzzleFlashPrefabs[UnityEngine.Random.Range(0, _muzzleFlashPrefabs.Length)];
        if (prefab == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        GameObject flash = Instantiate(prefab, origin, rotation, _muzzle);
        if (IsSniper)
        {
            flash.transform.localScale *= _sniperFlashScale;
        }

        Destroy(flash, _muzzleFlashTime);
    }

    // 화면 중앙으로 조준점을 찾고, 총구에서 그 점을 향해 검기(없으면 총알)를 쏜다.
    private void Fire()
    {
        bool useWaves = _swordWavePools != null && _swordWavePools.Length > 0;
        if (!useWaves && _bulletPool == null)
        {
            GameLogger.LogError("PlayerGunFire에 총알 풀이 없습니다. (Tools > Weapons > Build Bullet)");
            return;
        }

        Transform cameraTransform = Camera.main.transform;
        Ray aimRay = new Ray(cameraTransform.position, cameraTransform.forward);
        Vector3 aimPoint = Physics.Raycast(aimRay, out RaycastHit aimHit, _range, _hitMask, QueryTriggerInteraction.Ignore)
            ? aimHit.point
            : aimRay.GetPoint(_range);

        Vector3 origin = _muzzle != null ? _muzzle.position : cameraTransform.TransformPoint(_muzzleFallbackOffset);
        Vector3 direction = aimPoint - origin;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = cameraTransform.forward;
        }

        ShowMuzzleFlash(origin, direction);

        if (useWaves)
        {
            FireSwordWaves(origin, direction);
            return;
        }

        PooledObject pooled = _bulletPool.Spawn(origin, Quaternion.LookRotation(direction));
        if (pooled.TryGetComponent(out Bullet bullet))
        {
            bullet.Fire(origin, direction.normalized, IsSniper ? _sniperDamage : _damage, _hitMask, _hitEffectPool, _bloodEffectPool, IsSniper);
        }
    }

    // 검기 종류 중 이번에 나갈 개수만큼을 겹치지 않게 무작위로 골라 한꺼번에 날린다.
    private void FireSwordWaves(Vector3 origin, Vector3 direction)
    {
        int poolCount = _swordWavePools.Length;
        int min = Mathf.Clamp(_waveCountMin, 1, poolCount);
        int max = Mathf.Clamp(_waveCountMax, min, poolCount);
        int count = UnityEngine.Random.Range(min, max + 1);

        if (_waveOrder == null || _waveOrder.Length != poolCount)
        {
            _waveOrder = new int[poolCount];
            for (int i = 0; i < poolCount; i++)
            {
                _waveOrder[i] = i;
            }
        }

        Quaternion aim = Quaternion.LookRotation(direction);
        float damage = IsSniper ? _sniperDamage : _damage;

        // 부분 피셔-예이츠: 앞쪽 count개가 겹치지 않는 무작위 선택이 된다.
        for (int i = 0; i < count; i++)
        {
            int pick = UnityEngine.Random.Range(i, poolCount);
            (_waveOrder[i], _waveOrder[pick]) = (_waveOrder[pick], _waveOrder[i]);

            EffectPool pool = _swordWavePools[_waveOrder[i]];
            if (pool == null)
            {
                continue;
            }

            Quaternion rotation = aim;
            if (count > 1)
            {
                rotation *= Quaternion.Euler(
                    UnityEngine.Random.Range(-_waveSpreadAngle, _waveSpreadAngle),
                    UnityEngine.Random.Range(-_waveSpreadAngle, _waveSpreadAngle),
                    0f);
            }

            Vector3 waveDirection = rotation * Vector3.forward;
            PooledObject pooled = pool.Spawn(origin, rotation);
            if (pooled.TryGetComponent(out Bullet wave))
            {
                wave.Fire(origin, waveDirection, damage, _hitMask, _hitEffectPool, _bloodEffectPool);
            }
        }
    }
}
