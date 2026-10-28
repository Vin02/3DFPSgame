using UnityEngine;

// 등불처럼 빛의 세기를 불규칙하게 흔든다. 가끔 짧게 꺼졌다 켜진다.
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [SerializeField] private float _minIntensity = 0.4f;
    [SerializeField] private float _maxIntensity = 1.6f;
    [SerializeField] private float _speed = 6f;
    [Tooltip("초당 잠깐 꺼질 확률")]
    [SerializeField] private float _dropoutChance = 0.15f;
    [SerializeField] private float _dropoutDuration = 0.08f;

    private Light _light;
    private float _seed;
    private float _dropoutTimer;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _seed = Random.value * 100f;
    }

    private void Update()
    {
        if (_dropoutTimer > 0f)
        {
            _dropoutTimer -= Time.deltaTime;
            _light.intensity = _minIntensity * 0.2f;
            return;
        }

        if (Random.value < _dropoutChance * Time.deltaTime)
        {
            _dropoutTimer = _dropoutDuration;
        }

        float noise = Mathf.PerlinNoise(_seed, Time.time * _speed);
        _light.intensity = Mathf.Lerp(_minIntensity, _maxIntensity, noise);
    }
}
