using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 던져진 섬광탄. 처음 부딪히거나 신관 시간이 지나면 불이 붙어, 번쩍인 뒤 조명탄처럼 타면서 주변을 환하게 밝힌다.
// 다 타면 서서히 어두워지고 사라진다.
// 피해: 불이 붙는 순간 주변 좀비에게 한 번 크게(멀수록 약하게), 타는 동안에는 가까운 좀비에게 일정 간격으로 계속 준다.
// 사라지기 5초 전에는 한 번 더 폭발한다: 보이는 행성이 크게 부풀고, 그만큼 넓은 반경에 같은 피해를 주며, 빛은 꺼진다. (폭발 이펙트는 아직 없다)
[RequireComponent(typeof(Rigidbody))]
public class FlashbangProjectile : MonoBehaviour
{
    // 폭발 때 행성 둘레에 터뜨릴 이펙트 하나. 크기는 행성 반지름 4m 기준이다.
    [System.Serializable]
    private struct ExplosionEffect
    {
        public GameObject Prefab;
        public float Scale;
    }

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Tooltip("HUD에 보일 이름")]
    [SerializeField] private string _displayName = "Flashbang";
    [SerializeField] private Light _light;
    [Tooltip("불이 붙으면 빛나는 부분")]
    [SerializeField] private Renderer _glowRenderer;
    [SerializeField] private int _glowMaterialIndex = 1;
    [SerializeField] private Color _glowColor = new Color(1f, 0.95f, 0.85f);

    [Header("타이밍")]
    [Tooltip("부딪히지 않아도 이 시간(초)이 지나면 불이 붙는다.")]
    [SerializeField] private float _fuseTime = 1.5f;
    [SerializeField] private float _flashTime = 0.25f;
    [SerializeField] private float _burnTime = 8f;
    [SerializeField] private float _fadeTime = 2f;

    [Header("피해")]
    [SerializeField] private float _burstDamage = 150f;
    [SerializeField] private float _burstRadius = 9f;
    [Tooltip("가장자리에서 맞는 피해 비율 (가운데는 1)")]
    [SerializeField] private float _burstEdgeRatio = 0.4f;
    [Tooltip("타는 동안 초당 피해")]
    [SerializeField] private float _burnDamagePerSecond = 40f;
    [SerializeField] private float _burnRadius = 6f;
    [SerializeField] private float _damageInterval = 0.5f;

    [Tooltip("사라지기 전 폭발: 행성이 이 배율로 커지고, 피해 반경도 Burst Radius × 이 값이 된다. (피해량은 Burst Damage와 같다)")]
    [SerializeField] private float _explosionRadiusMultiplier = 50f;
    [Tooltip("다 사라지기 이만큼(초) 전에 폭발한다.")]
    [SerializeField] private float _explosionLeadTime = 5f;
    [Tooltip("행성이 커지는 데 걸리는 시간(초)")]
    [SerializeField] private float _growTime = 0.4f;

    [Header("폭발 이펙트 (행성 색과 같은 계열)")]
    [SerializeField] private ExplosionEffect[] _explosionEffects;
    [Tooltip("폭발 때 터뜨리는 이펙트 개수")]
    [SerializeField] private int _effectCount = 9;
    [Tooltip("행성 중심에서 이펙트까지 거리 = 부푼 행성 반지름 × 이 값. 1보다 커야 행성에 가려지지 않는다.")]
    [SerializeField] private float _effectDistance = 1.5f;
    [Tooltip("플레이어(카메라) 쪽으로 얼마나 몰아서 터뜨릴지. 0 = 사방 고르게, 1 = 대부분 플레이어 쪽")]
    [Range(0f, 1f)]
    [SerializeField] private float _effectViewerBias = 0.6f;
    [Tooltip("이펙트가 하나씩 터지는 간격(초). 파바박 터지는 느낌")]
    [SerializeField] private float _effectInterval = 0.05f;
    [Tooltip("이펙트가 이 시간(초) 뒤엔 반드시 사라진다.")]
    [SerializeField] private float _effectMaxLife = 5f;

    [Header("빛")]
    [SerializeField] private float _flashIntensity = 250f;
    [SerializeField] private float _burnIntensity = 180f;
    [SerializeField] private float _flickerAmount = 0.15f;
    [SerializeField] private float _flickerSpeed = 12f;
    [Tooltip("두 색 사이를 오가며 빛이 변한다. 속도가 0이면 라이트에 정해 둔 색 하나만 쓴다.")]
    [SerializeField] private Color _lightColor = Color.white;
    [SerializeField] private Color _lightColorAlt = Color.white;
    [SerializeField] private float _colorShiftSpeed = 0f;
    [Tooltip("빛을 깡통보다 이만큼 위(월드 기준)에 둔다. 땅에 붙은 빛은 땅을 거의 비추지 못하기 때문.")]
    [SerializeField] private float _lightHeight = 1.2f;

    public string DisplayName => _displayName;

    private Rigidbody _body;
    private MaterialPropertyBlock _glowBlock;
    private float _timer;
    private bool _ignited;
    private float _seed;
    private float _damageTimer;
    private bool _exploded;
    private float _growTimer;
    private Transform _visual;
    private Vector3 _visualScale;
    private readonly Collider[] _overlaps = new Collider[256];
    private readonly List<Zombie> _victims = new List<Zombie>(16);

    private void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _glowBlock = new MaterialPropertyBlock();
        _seed = Random.value * 100f;

        if (_light == null)
        {
            Debug.LogError($"{name}: FlashbangProjectile에 Light가 연결되지 않았습니다.");
            enabled = false;
            return;
        }

        _light.enabled = false;
        SetGlow(0f);

        // 폭발할 때 부풀릴 보이는 부분. (충돌 구는 그대로 둔다. 같이 키우면 땅에 끼어 튀어 오른다)
        _visual = transform.Find("Visual");
        if (_visual != null)
        {
            _visualScale = _visual.localScale;
        }
    }

    public void Launch(Vector3 velocity)
    {
        _body.linearVelocity = velocity;
        _body.angularVelocity = Random.insideUnitSphere * 10f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_ignited)
        {
            Ignite();
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        if (!_ignited)
        {
            if (_timer >= _fuseTime)
            {
                Ignite();
            }
            return;
        }

        // 타는 동안(번쩍임 뒤부터 다 탈 때까지) 가까운 좀비에게 주기적으로 피해
        if (_timer >= _flashTime && _timer < _flashTime + _burnTime)
        {
            _damageTimer += Time.deltaTime;
            if (_damageTimer >= _damageInterval)
            {
                _damageTimer -= _damageInterval;
                DamageZombies(_burnRadius, _burnDamagePerSecond * _damageInterval, 1f);
            }
        }

        // 다 사라지기 _explosionLeadTime 전에 한 번 폭발한다.
        float lifetime = _flashTime + _burnTime + _fadeTime;
        if (!_exploded && _timer >= lifetime - _explosionLeadTime)
        {
            _exploded = true;
            DamageZombies(_burstRadius * _explosionRadiusMultiplier, _burstDamage, _burstEdgeRatio);
            _light.enabled = false; // 폭발하고 나면 빛은 꺼진다.
            StartCoroutine(SpawnExplosionEffects());

            // 폭발한 뒤에는 더 구르거나 돌지 않는다.
            if (!_body.isKinematic)
            {
                _body.angularVelocity = Vector3.zero;
            }

            _body.constraints |= RigidbodyConstraints.FreezeRotation;
        }

        // 폭발한 뒤에는 행성이 부푼다. (사라지는 대신 반경이 커진다)
        if (_exploded && _visual != null && _growTimer < _growTime)
        {
            _growTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_growTimer / _growTime);
            _visual.localScale = _visualScale * Mathf.Lerp(1f, _explosionRadiusMultiplier, 1f - (1f - t) * (1f - t));
        }

        float brightness = GetBrightness(_timer);
        if (brightness <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (_exploded)
        {
            return;
        }

        // 깡통이 굴러도 빛은 항상 바로 위에 있게 한다.
        _light.transform.position = transform.position + Vector3.up * _lightHeight;

        // 조명탄처럼 빛이 조금씩 일렁인다.
        float flicker = 1f + (Mathf.PerlinNoise(_seed, _timer * _flickerSpeed) - 0.5f) * 2f * _flickerAmount;
        _light.intensity = brightness * flicker;
        if (_colorShiftSpeed > 0f)
        {
            _light.color = Color.Lerp(_lightColor, _lightColorAlt, 0.5f + 0.5f * Mathf.Sin(_timer * _colorShiftSpeed));
        }

        SetGlow(brightness / _burnIntensity * flicker);
    }

    // 부푼 행성 둘레(특히 플레이어 쪽)에 이펙트를 차례로 터뜨린다. 행성 표면보다 바깥에 두어 행성에 가려지지 않게 한다.
    private IEnumerator SpawnExplosionEffects()
    {
        if (_explosionEffects == null || _explosionEffects.Length == 0)
        {
            yield break;
        }

        // 행성이 다 부풀었을 때의 반지름(충돌 구 반지름 × 배율). 이펙트 크기도 반지름 4m 기준으로 맞춘다.
        var sphere = GetComponent<SphereCollider>();
        float baseRadius = sphere != null ? sphere.radius * transform.lossyScale.x : 0.08f;
        float radius = baseRadius * _explosionRadiusMultiplier;
        float sizeFactor = radius / 4f;
        Vector3 center = transform.position;
        Transform viewer = Camera.main != null ? Camera.main.transform : null;

        for (int i = 0; i < _effectCount; i++)
        {
            ExplosionEffect entry = _explosionEffects[i % _explosionEffects.Length];
            if (entry.Prefab == null)
            {
                continue;
            }

            // 방향: 사방 무작위에 플레이어 쪽을 섞는다.
            Vector3 direction = Random.onUnitSphere;
            if (viewer != null)
            {
                Vector3 toViewer = (viewer.position - center).normalized;
                direction = Vector3.Slerp(direction, toViewer, _effectViewerBias).normalized;
            }

            Vector3 position = center + direction * (radius * _effectDistance);
            Quaternion rotation = Quaternion.LookRotation(direction) * entry.Prefab.transform.rotation;
            GameObject effect = Instantiate(entry.Prefab, position, rotation);
            effect.transform.localScale *= entry.Scale * sizeFactor;
            LimitLifetime(effect);

            yield return new WaitForSeconds(_effectInterval);
        }
    }

    // 이펙트 프리팹은 대부분 반복 재생이라, 한 번만 재생되게 하고 끝나면 지운다.
    private void LimitLifetime(GameObject effect)
    {
        float life = 0f;
        foreach (ParticleSystem system in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            life = Mathf.Max(life, main.duration + main.startLifetime.constantMax);
        }

        Destroy(effect, Mathf.Min(life + 0.5f, _effectMaxLife));
    }

    private void Ignite()
    {
        _ignited = true;
        _timer = 0f;
        _light.enabled = true;
        DamageZombies(_burstRadius, _burstDamage, _burstEdgeRatio);
    }

    // 반경 안의 좀비에게 한 번씩 피해를 준다. 거리에 따라 1 → edgeRatio로 줄어든다.
    private void DamageZombies(float radius, float damage, float edgeRatio)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, _overlaps, ~0, QueryTriggerInteraction.Ignore);
        _victims.Clear();
        for (int i = 0; i < count; i++)
        {
            Zombie zombie = _overlaps[i].GetComponentInParent<Zombie>();
            if (zombie == null || zombie.IsDead || _victims.Contains(zombie))
            {
                continue;
            }

            _victims.Add(zombie);
            float distance = Vector3.Distance(transform.position, zombie.transform.position);
            zombie.TakeDamage(damage * Mathf.Lerp(1f, edgeRatio, Mathf.Clamp01(distance / radius)));
        }
    }

    // 불이 붙은 뒤 시간에 따른 빛의 세기: 번쩍임 → 타는 동안 유지 → 서서히 꺼짐
    private float GetBrightness(float time)
    {
        if (time < _flashTime)
        {
            return Mathf.Lerp(_flashIntensity, _burnIntensity, time / _flashTime);
        }

        float burnEnd = _flashTime + _burnTime;
        if (time < burnEnd)
        {
            return _burnIntensity;
        }

        float fade = (time - burnEnd) / _fadeTime;
        return fade >= 1f ? 0f : Mathf.Lerp(_burnIntensity, 0f, fade * fade);
    }

    private void SetGlow(float amount)
    {
        if (_glowRenderer == null)
        {
            return;
        }

        _glowRenderer.GetPropertyBlock(_glowBlock, _glowMaterialIndex);
        _glowBlock.SetColor(EmissionColorId, _glowColor * (amount * 8f));
        _glowRenderer.SetPropertyBlock(_glowBlock, _glowMaterialIndex);
    }
}
