using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// F3: 모든 조명 + 포스트 프로세싱 OFF / F4: 원래대로 ON
// O : 최적화 적용(조명·URP·LOD·애니메이터·스크립트) / P: 최적화 해제
// 씬 수정 없이 게임 시작 시 자동 생성된다. F8: 통계 오버레이 on/off, F9: 현재 통계를 로그/CSV로 기록.
public class RenderToggleController : SingletonBehaviour<RenderToggleController>
{
    private const float SweepInterval = 0.5f;
    private const float LightCullDistance = 30f;
    private const int SampleWindow = 300;

    private readonly struct LightState
    {
        public readonly Light Light;
        public readonly bool Enabled;
        public readonly LightShadows Shadows;
        public LightState(Light l) { Light = l; Enabled = l.enabled; Shadows = l.shadows; }
    }

    private readonly Dictionary<Light, LightState> _lightStates = new();
    private readonly Dictionary<Behaviour, bool> _volumeStates = new();
    private readonly Dictionary<Animator, AnimatorCullingMode> _animStates = new();
    private readonly Dictionary<Behaviour, bool> _flickerStates = new();
    private readonly List<ScriptableRendererFeature> _features = new();
    private readonly List<bool> _featureStates = new();
    private readonly List<Light> _lightBuffer = new();
    private readonly float[] _frameTimes = new float[SampleWindow];

    private bool _lightsPostOff;
    private bool _optimized;
    private bool _showStats = true;
    private float _sweepTimer;
    private int _sampleCount;
    private string _status = "";
    private string _overlayText = "";
    private GUIStyle _overlayStyle;

    private ProfilerRecorder _batches, _setPass, _tris, _drawCalls, _gcAlloc, _sysMem;

    // 원본 설정 백업
    private float _ambientIntensity, _reflectionIntensity, _shadowDistance, _lodBias;
    private int _msaa, _shadowCascades, _pixelLights, _skinWeights, _maxLod;
    private bool _softParticles, _realtimeReflection;
    private AnisotropicFiltering _aniso;
    private float _urpRenderScale, _urpShadowDist;
    private int _urpMsaa, _urpCascades, _urpMainShadowRes, _urpAddShadowRes;
    private bool _urpHdr, _urpSoft;
    private int _urpAddLights;
    private bool _backedUp;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn() => CreatePersistent("[RenderToggleController]");

    // 포인트 라이트 범위를 제한해 Forward+ 타일당 라이트 수를 줄인다(씬 파일은 건드리지 않고 실행 중에만 적용).
    private const float MaxPointLightRange = 22f;

    private void Start()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Point && l.range > MaxPointLightRange)
            {
                l.range = MaxPointLightRange;
            }
        }
    }

    private void OnEnable()
    {
        _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        _tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        _sysMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
    }

    private void OnDisable()
    {
        _batches.Dispose(); _setPass.Dispose(); _tris.Dispose();
        _drawCalls.Dispose(); _gcAlloc.Dispose(); _sysMem.Dispose();
        RestoreAll();
    }

    private void Update()
    {
        _frameTimes[_sampleCount++ % SampleWindow] = Time.unscaledDeltaTime;

        if (Input.GetKeyDown(KeyCode.F3)) SetLightsAndPost(false);
        if (Input.GetKeyDown(KeyCode.F4)) SetLightsAndPost(true);
        if (Input.GetKeyDown(KeyCode.O)) SetOptimized(true);
        if (Input.GetKeyDown(KeyCode.P)) SetOptimized(false);
        if (Input.GetKeyDown(KeyCode.F8)) _showStats = !_showStats;
        if (Input.GetKeyDown(KeyCode.F9)) Debug.Log(BuildStatsLine("SNAPSHOT"));

        _sweepTimer += Time.unscaledDeltaTime;
        if (_sweepTimer >= SweepInterval)
        {
            _sweepTimer = 0f;
            if (_showStats) RefreshOverlayText();
            if (_lightsPostOff) DisableNewLights();
            if (_optimized) CullDistantLights();
        }
    }

    // ───────── F3 / F4 ─────────
    private void SetLightsAndPost(bool on)
    {
        if (!on == _lightsPostOff) return;
        EnsureBackup();
        _lightsPostOff = !on;

        if (!on)
        {
            _lightStates.Clear();
            _volumeStates.Clear();
            DisableNewLights();
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                _volumeStates[v] = v.enabled;
                v.enabled = false;
            }
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.reflectionIntensity = 0f;
            SetFeatures(false);
        }
        else
        {
            foreach (var kv in _lightStates)
            {
                if (kv.Key == null) continue;
                kv.Key.enabled = kv.Value.Enabled;
                kv.Key.shadows = kv.Value.Shadows;
            }
            foreach (var kv in _volumeStates) if (kv.Key != null) kv.Key.enabled = kv.Value;
            RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.reflectionIntensity = _reflectionIntensity;
            SetFeatures(true);
        }
        ApplyCameraPost(on);
        _status = on ? "조명+포스트 ON (F4)" : "조명+포스트 OFF (F3)";
        Debug.Log(BuildStatsLine(_status));
    }

    private void DisableNewLights()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (!_lightStates.ContainsKey(l)) _lightStates[l] = new LightState(l);
            l.enabled = false;
        }
    }

    private static void ApplyCameraPost(bool on)
    {
        foreach (var c in Camera.allCameras)
        {
            var data = c.GetUniversalAdditionalCameraData();
            if (data == null) continue;
            data.renderPostProcessing = on;
            data.antialiasing = on ? data.antialiasing : AntialiasingMode.None;
        }
    }

    // 렌더러 피처(SSAO, 풀스크린 패스 등) 토글. 원래 상태 기억.
    private void SetFeatures(bool on)
    {
        if (!on)
        {
            _features.Clear(); _featureStates.Clear();
            foreach (var rd in GetRendererDatas())
            {
                foreach (var f in rd.rendererFeatures)
                {
                    if (f == null) continue;
                    _features.Add(f); _featureStates.Add(f.isActive);
                    f.SetActive(false);
                }
            }
        }
        else
        {
            for (int i = 0; i < _features.Count; i++)
                if (_features[i] != null) _features[i].SetActive(_featureStates[i]);
        }
    }

    private static IEnumerable<ScriptableRendererData> GetRendererDatas()
    {
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null) yield break;
        var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(asset) is ScriptableRendererData[] list)
            foreach (var d in list) if (d != null) yield return d;
    }

    // ───────── O / P ─────────
    private void SetOptimized(bool on)
    {
        if (on == _optimized) return;
        EnsureBackup();
        _optimized = on;
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        if (on)
        {
            // 1) 그림자: 거리/캐스케이드/해상도/소프트 축소
            QualitySettings.shadowDistance = 25f;
            QualitySettings.shadowCascades = 2;
            if (urp != null)
            {
                urp.shadowDistance = 25f;
                urp.shadowCascadeCount = 2;
                urp.mainLightShadowmapResolution = 1024;
                urp.additionalLightsShadowmapResolution = 512;
                urp.msaaSampleCount = 1;          // MSAA 4x -> 끔 (FXAA/기본으로 대체)
                urp.renderScale = 0.85f;
                urp.supportsHDR = false;
                urp.maxAdditionalLightsCount = 4; // 오브젝트당 추가 라이트 상한
            }
            // 2) 추가 라이트(점/스팟) 그림자 제거 → 그림자 패스 대폭 감소
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional) l.shadows = LightShadows.None;
            }
            // 3) 에셋/LOD/텍스처
            QualitySettings.lodBias = 0.6f;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.skinWeights = SkinWeights.TwoBones;
            QualitySettings.pixelLightCount = 2;
            // 4) 애니메이터: 화면 밖이면 평가 중단
            _animStates.Clear();
            foreach (var a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                _animStates[a] = a.cullingMode;
                a.cullingMode = AnimatorCullingMode.CullCompletely;
            }
            // 5) 스크립트: 매 프레임 조명 흔들림 중단(고정 세기)
            _flickerStates.Clear();
            foreach (var f in FindObjectsByType<LightFlicker>(FindObjectsSortMode.None))
            {
                _flickerStates[f] = f.enabled;
                f.enabled = false;
            }
            CullDistantLights();
            _status = "최적화 ON (O)";
        }
        else
        {
            RestoreOptimization(urp);
            _status = "최적화 OFF (P)";
        }
        Debug.Log(BuildStatsLine(_status));
    }

    // 카메라에서 먼 포인트/스팟 라이트는 끈다(F3 상태에서는 건드리지 않음).
    private void CullDistantLights()
    {
        if (_lightsPostOff) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        float sqr = LightCullDistance * LightCullDistance;
        _lightBuffer.Clear();
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional) continue;
            if (!_lightStates.ContainsKey(l)) _lightStates[l] = new LightState(l);
            l.enabled = (l.transform.position - p).sqrMagnitude < sqr && _lightStates[l].Enabled;
        }
    }

    private void RestoreOptimization(UniversalRenderPipelineAsset urp)
    {
        QualitySettings.shadowDistance = _shadowDistance;
        QualitySettings.shadowCascades = _shadowCascades;
        QualitySettings.lodBias = _lodBias;
        QualitySettings.anisotropicFiltering = _aniso;
        QualitySettings.softParticles = _softParticles;
        QualitySettings.realtimeReflectionProbes = _realtimeReflection;
        QualitySettings.skinWeights = (SkinWeights)_skinWeights;
        QualitySettings.pixelLightCount = _pixelLights;
        if (urp != null)
        {
            urp.shadowDistance = _urpShadowDist;
            urp.shadowCascadeCount = _urpCascades;
            urp.mainLightShadowmapResolution = _urpMainShadowRes;
            urp.additionalLightsShadowmapResolution = _urpAddShadowRes;
            urp.msaaSampleCount = _urpMsaa;
            urp.renderScale = _urpRenderScale;
            urp.supportsHDR = _urpHdr;
            urp.maxAdditionalLightsCount = _urpAddLights;
        }
        foreach (var kv in _lightStates)
        {
            if (kv.Key == null) continue;
            if (!_lightsPostOff) kv.Key.enabled = kv.Value.Enabled;
            kv.Key.shadows = kv.Value.Shadows;
        }
        foreach (var kv in _animStates) if (kv.Key != null) kv.Key.cullingMode = kv.Value;
        foreach (var kv in _flickerStates) if (kv.Key != null) kv.Key.enabled = kv.Value;
    }

    // ───────── 백업/복원 ─────────
    private void EnsureBackup()
    {
        if (_backedUp) return;
        _backedUp = true;
        _ambientIntensity = RenderSettings.ambientIntensity;
        _reflectionIntensity = RenderSettings.reflectionIntensity;
        _shadowDistance = QualitySettings.shadowDistance;
        _shadowCascades = QualitySettings.shadowCascades;
        _lodBias = QualitySettings.lodBias;
        _aniso = QualitySettings.anisotropicFiltering;
        _softParticles = QualitySettings.softParticles;
        _realtimeReflection = QualitySettings.realtimeReflectionProbes;
        _skinWeights = (int)QualitySettings.skinWeights;
        _pixelLights = QualitySettings.pixelLightCount;
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            _urpShadowDist = urp.shadowDistance;
            _urpCascades = urp.shadowCascadeCount;
            _urpMainShadowRes = urp.mainLightShadowmapResolution;
            _urpAddShadowRes = urp.additionalLightsShadowmapResolution;
            _urpSoft = urp.supportsSoftShadows;
            _urpMsaa = urp.msaaSampleCount;
            _urpRenderScale = urp.renderScale;
            _urpHdr = urp.supportsHDR;
            _urpAddLights = urp.maxAdditionalLightsCount;
        }
    }

    // 종료 시 에디터 에셋이 바뀐 채로 남지 않게 모두 복원
    private void RestoreAll()
    {
        if (!_backedUp) return;
        if (_optimized) { _optimized = false; RestoreOptimization(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset); }
        if (_lightsPostOff) { _lightsPostOff = false; SetFeatures(true); ApplyCameraPost(true); RenderSettings.ambientIntensity = _ambientIntensity; RenderSettings.reflectionIntensity = _reflectionIntensity; }
    }

    // ───────── 통계 ─────────
    private (float avgMs, float p95Ms, float maxMs) FrameStats()
    {
        int n = Mathf.Min(_sampleCount, SampleWindow);
        if (n == 0) return (0, 0, 0);
        var tmp = new float[n];
        float sum = 0f, max = 0f;
        for (int i = 0; i < n; i++) { tmp[i] = _frameTimes[i]; sum += tmp[i]; if (tmp[i] > max) max = tmp[i]; }
        System.Array.Sort(tmp);
        return (sum / n * 1000f, tmp[Mathf.Min(n - 1, (int)(n * 0.95f))] * 1000f, max * 1000f);
    }

    private int ActiveLightCount()
    {
        int c = 0;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.enabled && l.gameObject.activeInHierarchy) c++;
        return c;
    }

    private string BuildStatsLine(string tag)
    {
        var (avg, p95, max) = FrameStats();
        var sb = new StringBuilder();
        sb.Append("[RenderToggle] ").Append(tag)
          .Append(" | frame avg=").Append(avg.ToString("F2")).Append("ms (").Append((1000f / Mathf.Max(avg, 0.01f)).ToString("F0")).Append("fps)")
          .Append(" p95=").Append(p95.ToString("F2")).Append(" max=").Append(max.ToString("F2"))
          .Append(" | batches=").Append(_batches.LastValue).Append(" setpass=").Append(_setPass.LastValue)
          .Append(" draw=").Append(_drawCalls.LastValue).Append(" tris=").Append(_tris.LastValue)
          .Append(" | GC/frame=").Append(_gcAlloc.LastValue).Append("B sysMem=").Append(_sysMem.LastValue / (1024 * 1024)).Append("MB")
          .Append(" | activeLights=").Append(ActiveLightCount());
        return sb.ToString();
    }

    // 오버레이 문구는 0.5초마다 한 번만 만든다. OnGUI는 프레임당 여러 번 호출되므로 할당 금지.
    private void RefreshOverlayText()
    {
        var (avg, p95, _) = FrameStats();
        _overlayText =
            $"Lights+Post: {(_lightsPostOff ? "OFF" : "ON")}   Optimize: {(_optimized ? "ON" : "OFF")}\n" +
            $"{avg:F2}ms ({1000f / Mathf.Max(avg, 0.01f):F0}fps) p95 {p95:F2}ms\n" +
            $"batches {_batches.LastValue}  setpass {_setPass.LastValue}  tris {_tris.LastValue}\n" +
            $"lights {ActiveLightCount()}  GC {_gcAlloc.LastValue}B\n" +
            "F3/F4 light+post | O/P optimize | F8 overlay | F9 log";
    }

    private void OnGUI()
    {
        if (!_showStats) return;
        _overlayStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.yellow } };
        GUI.Box(new Rect(8, 8, 360, 100), GUIContent.none);
        GUI.Label(new Rect(14, 10, 350, 96), _overlayText, _overlayStyle);
    }
}
