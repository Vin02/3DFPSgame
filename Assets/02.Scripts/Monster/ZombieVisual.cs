using UnityEngine;

// 좀비 몸체(프리미티브로 만든 휴머노이드)를 이동 속도에 맞춰 절차적으로 움직이는 스크립트
public class ZombieVisual : MonoBehaviour
{
    [Header("부위")]
    [SerializeField] private Transform _upperBody;
    [SerializeField] private Transform _head;
    [SerializeField] private Transform _leftArm;
    [SerializeField] private Transform _rightArm;
    [SerializeField] private Transform _leftLeg;
    [SerializeField] private Transform _rightLeg;

    [Header("애니메이션")]
    [SerializeField] private float _stepFrequency = 1.6f;   // 속도 1당 걸음 주기
    [SerializeField] private float _legSwing = 32f;
    [SerializeField] private float _armRaise = -75f;        // 팔을 앞으로 뻗은 기본 각도
    [SerializeField] private float _armSway = 10f;
    [SerializeField] private float _slouch = 14f;

    private CharacterController _controller;
    private float _phase;
    private float _seed;
    private float _moveBlend;

    private void Awake()
    {
        _controller = GetComponentInParent<CharacterController>();
        _seed = Random.value * 100f;
        _phase = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        Vector3 v = _controller != null ? _controller.velocity : Vector3.zero;
        v.y = 0f;
        float speed = v.magnitude;

        _moveBlend = Mathf.MoveTowards(_moveBlend, Mathf.Clamp01(speed / 1.5f), Time.deltaTime * 4f);
        _phase += Time.deltaTime * (1.5f + speed * _stepFrequency);

        float s = Mathf.Sin(_phase);
        float t = Time.time + _seed;

        // 다리: 이동 중에만 교차로 흔든다.
        float leg = s * _legSwing * _moveBlend;
        SetX(_leftLeg, leg);
        SetX(_rightLeg, -leg);

        // 팔: 앞으로 뻗은 채 느릿하게 흔들린다. 이동 중이 아니어도 늘어진 듯 출렁인다.
        float armIdle = Mathf.Sin(t * 1.3f) * 4f;
        SetX(_leftArm, _armRaise + armIdle - s * _armSway * _moveBlend);
        SetX(_rightArm, _armRaise - armIdle + s * _armSway * _moveBlend);

        // 상체/머리: 구부정하게 좌우로 기우뚱
        if (_upperBody != null)
        {
            _upperBody.localRotation = Quaternion.Euler(
                _slouch + Mathf.Abs(s) * 3f * _moveBlend,
                0f,
                Mathf.Sin(t * 0.9f) * 2f + s * 4f * _moveBlend);
        }

        if (_head != null)
        {
            _head.localRotation = Quaternion.Euler(
                Mathf.Sin(t * 0.7f) * 5f - 6f,
                Mathf.Sin(t * 0.5f) * 12f,
                Mathf.Sin(t * 1.1f) * 10f);
        }
    }

    private static void SetX(Transform part, float angle)
    {
        if (part != null)
        {
            part.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
