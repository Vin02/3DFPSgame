using System;
using UnityEngine;

// 플레이어의 이동 속도와 사격 입력을 Animator 파라미터로 전달한다.
// 사격 중에는 총열이 항상 모델 정면을 향하도록 척추를 돌려 보정한다.
// (하체 달리기와 상체 사격 클립의 골반 방향이 달라, 레이어를 합치면 상체가 틀어지기 때문)
// 폭탄 던지기는 Throw 레이어(왼팔만 덮음)의 왼팔 투척 클립을 ThrowTime(정규화 시간)으로 직접 재생한다.
// (클립은 Tools > Characters > Build Left Arm Throw가 Humanoid 근육 키프레임으로 만든다)
public class PlayerAnimation : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private static readonly int ThrowTimeHash = Animator.StringToHash("ThrowTime");

    private enum ThrowPhase
    {
        None,
        Charging,
        Swinging,
    }

    [SerializeField] private Animator _animator;
    [SerializeField] private CharacterController _controller;

    [Header("Aim")]
    [SerializeField] private Transform _spine;
    [SerializeField] private Transform _hand;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private float _aimBlendTime = 0.1f;

    [Header("Injured")]
    [SerializeField] private PlayerHealth _health;
    [SerializeField] private string _injuredLayerName = "Injured";
    [SerializeField] private float _injuredBlendTime = 0.5f;

    [Header("Throw")]
    [SerializeField] private string _throwLayerName = "Throw";
    // 클립 정규화 시간: 0 살짝 젖힘 → 0.2 최대로 젖힘 → 0.45 앞으로 뻗음(놓는 순간) → 0.7 마무리. (ThrowArmClipBuilder와 맞춘다)
    [Tooltip("힘을 모으기 시작할 때의 정규화 시간(살짝 젖힌 자세).")]
    [SerializeField] private float _throwWindUpStart = 0f;
    [Tooltip("힘을 최대로 모았을 때의 정규화 시간.")]
    [SerializeField] private float _throwPeak = 0.2f;
    [Tooltip("손에서 폭탄이 떠나는 정규화 시간. 이 순간 ThrowReleased가 발생한다.")]
    [SerializeField] private float _throwRelease = 0.45f;
    [Tooltip("힘을 모으며 팔을 젖히는 속도(정규화 시간/초).")]
    [SerializeField] private float _throwChargeSpeed = 0.5f;
    [Tooltip("놓은 뒤 던지기 동작을 재생하는 속도(정규화 시간/초).")]
    [SerializeField] private float _throwSwingSpeed = 2f;
    [Tooltip("총 든 자세와 던지기 자세 사이를 넘어가는 시간(초).")]
    [SerializeField] private float _throwBlendTime = 0.15f;
    [Tooltip("왼팔 레이어(Throw) 가중치. 낮출수록 총 든 자세와 섞여 팔 동작 폭이 줄어든다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _throwArmWeight = 1f;
    [Tooltip("던지기 동작을 끝내고 총 든 자세로 돌아가기 시작하는 정규화 시간.")]
    [SerializeField] private float _throwEnd = 0.7f;

    private float _aimWeight;
    private bool _canAim;
    private int _injuredLayer = -1;
    private float _injuredWeight;

    private int _throwLayer = -1;
    private ThrowPhase _throwPhase;
    private float _throwTime;
    private float _throwTarget;
    private float _throwBlend;
    private bool _throwReleased;

    // 던지기 동작에서 폭탄이 손을 떠나는 프레임에 한 번 발생한다.
    public event Action ThrowReleased;

    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }

        if (_controller == null)
        {
            _controller = GetComponentInParent<CharacterController>();
        }

        _injuredLayer = _animator.GetLayerIndex(_injuredLayerName);

        _throwLayer = _animator.GetLayerIndex(_throwLayerName);
        if (_throwLayer < 0)
        {
            GameLogger.LogWarning($"PlayerAnimation: Animator에 '{_throwLayerName}' 레이어가 없어 던지기 모션을 쓰지 않습니다. (Tools > Characters > Build Left Arm Throw)");
        }

        _canAim = _spine != null && _hand != null && _muzzle != null;
        if (!_canAim)
        {
            GameLogger.LogWarning("PlayerAnimation의 척추·손·총구 참조가 없어 사격 보정을 하지 않습니다.");
        }
    }

    private void Update()
    {
        Vector3 velocity = _controller.velocity;
        velocity.y = 0f;

        _animator.SetFloat(SpeedHash, velocity.magnitude, 0.1f, Time.deltaTime);
        _animator.SetBool(ShootHash, Input.GetButton("Fire1"));

        UpdateInjuredLayer();
        UpdateThrowLayer();
    }

    // 체력이 절반 이하이면 절뚝이는 레이어의 가중치를 올린다. 이동 중일 때만 레이어의 달리기 모션이 재생된다.
    private void UpdateInjuredLayer()
    {
        if (_health == null || _injuredLayer < 0)
        {
            return;
        }

        float target = _health.IsInjured ? 1f : 0f;
        _injuredWeight = Mathf.MoveTowards(_injuredWeight, target, Time.deltaTime / _injuredBlendTime);
        _animator.SetLayerWeight(_injuredLayer, _injuredWeight);
    }

    // 힘을 모으는 동안 호출한다. 왼팔을 총에서 떼어 charge01(0~1)만큼 뒤로 젖힌다. false로 부르면 모으기를 취소한다.
    public void SetThrowCharge(bool charging, float charge01)
    {
        if (_throwLayer < 0 || _throwPhase == ThrowPhase.Swinging)
        {
            return;
        }

        if (!charging)
        {
            _throwPhase = ThrowPhase.None;
            return;
        }

        if (_throwPhase == ThrowPhase.None)
        {
            _throwPhase = ThrowPhase.Charging;
            _throwTime = _throwWindUpStart;
        }

        _throwTarget = Mathf.Lerp(_throwWindUpStart, _throwPeak, Mathf.Clamp01(charge01));
    }

    // 놓는 순간 호출한다. 젖힌 자세에서 던지기 동작을 끝까지 재생하고, 중간에 ThrowReleased가 발생한다.
    // 모션을 쓸 수 없으면 false를 돌려주므로 호출한 쪽이 바로 던져야 한다.
    public bool PlayThrowSwing()
    {
        if (_throwLayer < 0)
        {
            return false;
        }

        if (_throwPhase != ThrowPhase.Swinging)
        {
            _throwTime = Mathf.Max(_throwTime, _throwWindUpStart);
            _throwPhase = ThrowPhase.Swinging;
            _throwReleased = false;
        }

        return true;
    }

    private void UpdateThrowLayer()
    {
        if (_throwLayer < 0)
        {
            return;
        }

        float targetBlend = _throwPhase == ThrowPhase.None ? 0f : 1f;
        _throwBlend = Mathf.MoveTowards(_throwBlend, targetBlend, Time.deltaTime / _throwBlendTime);

        switch (_throwPhase)
        {
            case ThrowPhase.Charging:
                _throwTime = Mathf.MoveTowards(_throwTime, _throwTarget, _throwChargeSpeed * Time.deltaTime);
                break;

            case ThrowPhase.Swinging:
                float previous = _throwTime;
                _throwTime = Mathf.Min(_throwEnd, _throwTime + _throwSwingSpeed * Time.deltaTime);

                if (!_throwReleased && previous < _throwRelease && _throwTime >= _throwRelease)
                {
                    _throwReleased = true;
                    ThrowReleased?.Invoke();
                }

                if (_throwTime >= _throwEnd)
                {
                    _throwPhase = ThrowPhase.None;
                }
                break;

            default:
                if (_throwBlend <= 0f)
                {
                    _throwTime = 0f;
                }
                break;
        }

        _animator.SetFloat(ThrowTimeHash, _throwTime);
        _animator.SetLayerWeight(_throwLayer, _throwBlend * _throwArmWeight);
    }

    // Animator가 포즈를 계산한 뒤에 척추를 돌려 총열을 정면으로 맞춘다.
    private void LateUpdate()
    {
        if (!_canAim)
        {
            return;
        }

        float target = Input.GetButton("Fire1") ? 1f : 0f;
        _aimWeight = Mathf.MoveTowards(_aimWeight, target, Time.deltaTime / _aimBlendTime);

        if (_aimWeight <= 0f)
        {
            return;
        }

        Vector3 barrel = transform.InverseTransformDirection(_muzzle.position - _hand.position);
        float yaw = Mathf.Atan2(barrel.x, barrel.z) * Mathf.Rad2Deg;

        _spine.rotation = Quaternion.AngleAxis(-yaw * _aimWeight, transform.up) * _spine.rotation;
    }
}
