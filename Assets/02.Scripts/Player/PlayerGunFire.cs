using UnityEngine;
using UnityEngine.Serialization;

//마우스 왼쪽 버튼 누르면 바라보는 방향으로 총알(Bullet 프리팹)을 발사
// 탄창 하나에 정해진 수만큼 쏠 수 있고, R 키(또는 빈 탄창으로 쏘려 할 때) 재장전한다. 탄창 수는 무제한이다.
public class PlayerGunFire : MonoBehaviour
{
    [Header("Magazine")]
    [SerializeField] private int _magazineSize = 30;
    [SerializeField] private float _reloadTime = 1.8f;
    [SerializeField] private KeyCode _reloadKey = KeyCode.R;

    private int _ammo;
    private float _reloadTimer;

    public int Ammo => _ammo;
    public int MagazineSize => _magazineSize;
    public bool IsReloading => _reloadTimer > 0f;
    // 0(시작) ~ 1(완료). 재장전 중이 아니면 0.
    public float ReloadProgress => IsReloading ? 1f - _reloadTimer / _reloadTime : 0f;

    [Header("Effects (풀)")]
    [SerializeField] private EffectPool _hitEffectPool;
    [SerializeField] private EffectPool _bloodEffectPool;
    [Tooltip("총알 프리팹(Bullet)을 담은 풀")]
    [SerializeField] private EffectPool _bulletPool;
    [Header("Bullet")]
    [Tooltip("총구 위치. 총을 든 손 본의 자식 오브젝트")]
    [SerializeField] private Transform _muzzle;
    [Tooltip("총구가 없을 때 쓰는 카메라 기준 시작 위치 (오른쪽, 위, 앞)")]
    [FormerlySerializedAs("_tracerStartOffset")]
    [SerializeField] private Vector3 _muzzleFallbackOffset = new Vector3(0.25f, -0.2f, 0.7f);
    [Tooltip("조준과 총알이 맞힐 수 있는 레이어 (플레이어 자신은 빼야 한다)")]
    [SerializeField] private LayerMask _hitMask = ~0;
    [Header("Stat")]
    [SerializeField] private float _range = 100f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _fireInterval = 0.1f;
    private float _fireTimer = 0f;

    private void Awake()
    {
        _ammo = _magazineSize;
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
                _ammo = _magazineSize;
            }
            return;
        }

        if (Input.GetKeyDown(_reloadKey) && _ammo < _magazineSize)
        {
            StartReload();
            return;
        }

        // 1. 마우스 왼쪽 버튼 입력 받기
        if (Input.GetButton("Fire1") && _fireTimer <= 0f)
        {
            if (_ammo <= 0)
            {
                StartReload();
                return;
            }

            _fireTimer = _fireInterval;
            _ammo--;
            Fire();
        }
    }

    private void StartReload()
    {
        _reloadTimer = _reloadTime;
    }

    // 화면 중앙으로 조준점을 찾고, 총구에서 그 점을 향해 총알을 쏜다.
    private void Fire()
    {
        if (_bulletPool == null)
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

        PooledObject pooled = _bulletPool.Spawn(origin, Quaternion.LookRotation(direction));
        if (pooled.TryGetComponent(out Bullet bullet))
        {
            bullet.Fire(origin, direction.normalized, _damage, _hitMask, _hitEffectPool, _bloodEffectPool);
        }
    }
}
