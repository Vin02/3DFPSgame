using UnityEngine;

// 플레이어가 거대 존재 쪽을 일정 시간(기본 5초) 이상 계속 바라본 뒤부터, 바라보는 각도에 따라 화면 글리치(Custom/ColossusGlitch)의 세기를 올린다.
// 세기는 전역 셰이더 값으로 넘긴다. (재질 에셋을 런타임에 고치지 않는다)
// 바라보는 각도는 카메라 정면과, 존재의 몸 한가운데를 향한 방향 사이로 잰다.
public class ColossusGlitch : MonoBehaviour
{
    private static readonly int GlitchId = Shader.PropertyToID("_ColossusGlitch");

    [SerializeField] private Transform _viewer;
    [Tooltip("몸 한가운데로 칠 지점(존재 위치 기준 높이)")]
    [SerializeField] private float _focusHeight = 350f;

    [Header("응시 시간")]
    [Tooltip("시야 각도(Start Angle) 안에서 이 시간(초) 동안 계속 바라봐야 효과가 시작된다. 눈을 돌리면 처음부터 다시 센다.")]
    [SerializeField] private float _gazeDelay = 5f;

    [Header("세기")]
    [Tooltip("이 각도(도) 안으로 들어오면 글리치가 시작된다.")]
    [SerializeField] private float _startAngle = 75f;
    [Tooltip("이 각도(도) 안이면 최대 세기")]
    [SerializeField] private float _fullAngle = 30f;
    [SerializeField] private float _maxIntensity = 0.75f;
    [SerializeField] private float _riseSharpness = 4f;
    [SerializeField] private float _fallSharpness = 7f;

    public float Intensity { get; private set; }

    // 지금까지 계속 바라본 시간(초)
    public float GazeTime { get; private set; }

    // 0~1로 맞춘 세기. 다른 연출(광기 효과)이 이 값을 쓴다.
    public float NormalizedIntensity => _maxIntensity > 0f ? Intensity / _maxIntensity : 0f;

    private void Awake()
    {
        if (_viewer == null)
        {
            GameLogger.LogError("ColossusGlitch에 _viewer(카메라)가 없습니다.");
            enabled = false;
        }
    }

    private void OnDisable()
    {
        Intensity = 0f;
        GazeTime = 0f;
        Shader.SetGlobalFloat(GlitchId, 0f);
    }

    private void Update()
    {
        Vector3 focus = transform.position + Vector3.up * _focusHeight;
        float angle = Vector3.Angle(_viewer.forward, focus - _viewer.position);
        float target = Mathf.Clamp01(Mathf.InverseLerp(_startAngle, _fullAngle, angle));
        target = target * target * (3f - 2f * target) * _maxIntensity;

        // 바라보는 동안만 시간을 센다. 시야 밖으로 나가면 처음부터.
        GazeTime = angle < _startAngle ? GazeTime + Time.unscaledDeltaTime : 0f;
        if (GazeTime < _gazeDelay)
        {
            target = 0f;
        }

        float sharpness = target > Intensity ? _riseSharpness : _fallSharpness;
        Intensity = Mathf.Lerp(Intensity, target, 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));
        Shader.SetGlobalFloat(GlitchId, Intensity);
    }
}
