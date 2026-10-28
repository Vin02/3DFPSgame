using UnityEngine;

// 던져진 섬광탄. 처음 부딪히거나 신관 시간이 지나면 불이 붙어, 번쩍인 뒤 조명탄처럼 타면서 주변을 환하게 밝힌다.
// 다 타면 서서히 어두워지고 사라진다. 폭발이나 피해는 없다.
[RequireComponent(typeof(Rigidbody))]
public class FlashbangProjectile : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

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

    [Header("빛")]
    [SerializeField] private float _flashIntensity = 250f;
    [SerializeField] private float _burnIntensity = 180f;
    [SerializeField] private float _flickerAmount = 0.15f;
    [SerializeField] private float _flickerSpeed = 12f;
    [Tooltip("빛을 깡통보다 이만큼 위(월드 기준)에 둔다. 땅에 붙은 빛은 땅을 거의 비추지 못하기 때문.")]
    [SerializeField] private float _lightHeight = 1.2f;

    private Rigidbody _body;
    private MaterialPropertyBlock _glowBlock;
    private float _timer;
    private bool _ignited;
    private float _seed;

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

        float brightness = GetBrightness(_timer);
        if (brightness <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // 깡통이 굴러도 빛은 항상 바로 위에 있게 한다.
        _light.transform.position = transform.position + Vector3.up * _lightHeight;

        // 조명탄처럼 빛이 조금씩 일렁인다.
        float flicker = 1f + (Mathf.PerlinNoise(_seed, _timer * _flickerSpeed) - 0.5f) * 2f * _flickerAmount;
        _light.intensity = brightness * flicker;
        SetGlow(brightness / _burnIntensity * flicker);
    }

    private void Ignite()
    {
        _ignited = true;
        _timer = 0f;
        _light.enabled = true;
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
