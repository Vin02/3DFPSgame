using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 거대 존재를 응시할 때 정신이 아득해지는 광기 연출. 광기 수치(0~1)는 ColossusGlitch가 시선 각도로 계산한 값을 그대로 쓴다.
//  - 렌즈 왜곡: 화면 가운데로 빨려 드는 오목한 시야
//  - 피사체 심도: 먼 곳이 뿌옇게 흐려진다. (광기가 높을 때만 눈에 띈다)
//  - 색 조정: 채도가 빠지고 색조가 틀어진다.
//  - 정신적 떨림: 카메라가 아주 빠르고 미세하게 떨린다. (타격이 아니라 몸이 파르르 떠는 느낌)
// 색수차·비네트는 ColossusGlitch 셰이더(색 번짐, 붉은 가장자리)가 이미 하므로 따로 넣지 않는다.
// 볼륨은 런타임에 만든 프로필 사본을 쓰며, 이 컴포넌트가 사라질 때 함께 지운다. (프로젝트의 볼륨 에셋은 건드리지 않는다)
[DefaultExecutionOrder(200)] // CameraFollow가 카메라 위치를 정한 뒤에 떨림을 얹는다.
public class MadnessEffect : MonoBehaviour
{
    [SerializeField] private ColossusGlitch _source;
    [SerializeField] private Transform _camera;

    [Header("렌즈 왜곡 (마이너스 = 오목)")]
    [SerializeField] private float _lensDistortion = -0.4f;

    [Header("피사체 심도 (가우시안)")]
    [Tooltip("광기가 최대일 때 흐림이 시작되는 거리(m)")]
    [SerializeField] private float _blurStart = 3f;
    [Tooltip("광기가 최대일 때 완전히 흐려지는 거리(m)")]
    [SerializeField] private float _blurEnd = 18f;
    [SerializeField] private float _blurMaxRadius = 1.2f;

    [Header("색 조정")]
    [SerializeField] private float _saturation = -55f;
    [SerializeField] private float _hueShift = 18f;

    [Header("정신적 떨림")]
    [Tooltip("광기가 최대일 때 카메라가 흔들리는 최대 거리(m)")]
    [SerializeField] private float _shakeAmount = 0.012f;
    [SerializeField] private float _shakeSpeed = 38f;

    private Volume _volume;
    private VolumeProfile _profile;
    private Vector3 _appliedOffset;
    private Vector3 _appliedPosition;

    private void Awake()
    {
        if (_source == null || _camera == null)
        {
            GameLogger.LogError("MadnessEffect에 필요한 참조가 없습니다.");
            enabled = false;
            return;
        }

        _profile = ScriptableObject.CreateInstance<VolumeProfile>();

        LensDistortion lens = _profile.Add<LensDistortion>();
        lens.intensity.Override(_lensDistortion);

        DepthOfField depth = _profile.Add<DepthOfField>();
        depth.mode.Override(DepthOfFieldMode.Gaussian);
        depth.gaussianStart.Override(_blurStart);
        depth.gaussianEnd.Override(_blurEnd);
        depth.gaussianMaxRadius.Override(_blurMaxRadius);

        ColorAdjustments color = _profile.Add<ColorAdjustments>();
        color.saturation.Override(_saturation);
        color.hueShift.Override(_hueShift);

        var volumeObject = new GameObject("MadnessVolume");
        volumeObject.transform.SetParent(transform, false);
        _volume = volumeObject.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 50;
        _volume.weight = 0f;
        _volume.sharedProfile = _profile;
    }

    private void OnDestroy()
    {
        if (_profile != null)
        {
            Destroy(_profile);
        }
    }

    private void LateUpdate()
    {
        float madness = _source.NormalizedIntensity;

        // 렌즈·색은 처음부터 서서히, 흐림은 광기가 높을 때만 느껴지도록 가중치 하나로 함께 섞는다.
        _volume.weight = madness;

        // 이전 프레임에 얹은 떨림이 그대로 남아 있으면(카메라 위치를 다른 스크립트가 다시 정하지 않았으면) 먼저 걷어낸다.
        if (_camera.position == _appliedPosition)
        {
            _camera.position -= _appliedOffset;
        }

        _appliedOffset = Vector3.zero;
        if (madness > 0.01f)
        {
            float time = Time.unscaledTime * _shakeSpeed;
            Vector3 noise = new Vector3(
                Mathf.PerlinNoise(time, 0.3f) - 0.5f,
                Mathf.PerlinNoise(0.7f, time) - 0.5f,
                Mathf.PerlinNoise(time, time * 0.5f + 9f) - 0.5f) * 2f;
            _appliedOffset = _camera.rotation * (noise * _shakeAmount * madness * madness);
            _camera.position += _appliedOffset;
        }

        _appliedPosition = _camera.position;
    }
}
