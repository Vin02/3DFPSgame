using UnityEngine;

// 좀비를 기괴한 코스믹 호러 존재로 보이게 하는 연출. 애니메이션(Animator)이 포즈를 만든 뒤에 덧입힌다.
//  - 틀린 비율: 원래 머리는 접어 없애고(얼굴 없음), 팔을 길게 늘인다.
//  - 경련: 애니메이션이 순간 멈췄다가 급하게 튀고, 머리(살덩이)가 뚝뚝 꺾인다.
//  - 눈: 몸과 머리에 돋은 눈들이 모두 플레이어(카메라)를 쳐다보고 제각각 깜빡인다.
//  - 아가리: 세로로 갈라진 입이 숨 쉬듯 열리고, 가까이 오면 크게 벌어진다.
// 머리 살덩이·눈·아가리·촉수 오브젝트는 GrotesqueZombieBuilder가 프리팹에 만들어 연결한다.
public class GrotesqueBody : MonoBehaviour
{
    [SerializeField] private Animator _animator;

    [Header("비율")]
    [SerializeField] private float _armStretch = 1.5f;
    [Tooltip("원래 머리 뼈의 크기. 0에 가까우면 머리가 사라져 얼굴이 없어진다.")]
    [SerializeField] private float _headBoneScale = 0.001f;

    [Header("머리 (살덩이)")]
    [SerializeField] private Transform _head;
    [SerializeField] private float _headSnapAngle = 55f;
    [SerializeField] private float _headSnapSharpness = 30f;

    [Header("아가리")]
    [SerializeField] private Transform _mawLeft;
    [SerializeField] private Transform _mawRight;
    [SerializeField] private float _mawOpenAngle = 38f;
    [SerializeField] private float _mawNearDistance = 14f;

    [Header("눈")]
    [SerializeField] private Transform[] _eyes;
    [SerializeField] private float _eyeLookLimit = 70f;
    [SerializeField] private float _eyeLookSharpness = 12f;
    [SerializeField] private float _blinkDuration = 0.12f;

    [Header("경련")]
    [Tooltip("정상 속도로 움직이는 시간 범위(초)")]
    [SerializeField] private Vector2 _calmTime = new Vector2(0.4f, 1.6f);
    [Tooltip("멈추거나 튀는 시간 범위(초)")]
    [SerializeField] private Vector2 _spasmTime = new Vector2(0.08f, 0.3f);
    [SerializeField] private float _surgeSpeed = 2.6f;

    private Transform _headBone;
    private Transform _leftLowerArm;
    private Transform _leftHand;
    private Transform _rightLowerArm;
    private Transform _rightHand;
    private Vector3 _leftLowerArmRest;
    private Vector3 _leftHandRest;
    private Vector3 _rightLowerArmRest;
    private Vector3 _rightHandRest;

    private Quaternion _headRest;
    private Quaternion _headOffset = Quaternion.identity;
    private Quaternion _headTarget = Quaternion.identity;
    private Quaternion _mawLeftRest;
    private Quaternion _mawRightRest;
    private float _mawOpen;

    private Quaternion[] _eyeRest;
    private Vector3[] _eyeScale;
    private float[] _blinkTimers;
    private float[] _blinkCooldowns;

    private Transform _viewer;
    private float _spasmTimer;
    private bool _inSpasm;

    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        if (_animator == null || !_animator.isHuman)
        {
            Debug.LogError($"{name}: GrotesqueBody에는 Humanoid Animator가 필요합니다.");
            enabled = false;
            return;
        }

        _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
        _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        _leftLowerArmRest = _leftLowerArm.localPosition;
        _leftHandRest = _leftHand.localPosition;
        _rightLowerArmRest = _rightLowerArm.localPosition;
        _rightHandRest = _rightHand.localPosition;

        if (_head != null)
        {
            _headRest = _head.localRotation;
        }

        if (_mawLeft != null && _mawRight != null)
        {
            _mawLeftRest = _mawLeft.localRotation;
            _mawRightRest = _mawRight.localRotation;
        }

        int eyeCount = _eyes != null ? _eyes.Length : 0;
        _eyeRest = new Quaternion[eyeCount];
        _eyeScale = new Vector3[eyeCount];
        _blinkTimers = new float[eyeCount];
        _blinkCooldowns = new float[eyeCount];
        for (int i = 0; i < eyeCount; i++)
        {
            _eyeRest[i] = _eyes[i].localRotation;
            _eyeScale[i] = _eyes[i].localScale;
            _blinkCooldowns[i] = Random.Range(0.5f, 5f);
        }

        _spasmTimer = Random.Range(_calmTime.x, _calmTime.y);
    }

    private void Start()
    {
        // 눈이 쳐다볼 대상. 카메라는 한 번만 찾아 둔다.
        Camera viewerCamera = Camera.main;
        _viewer = viewerCamera != null ? viewerCamera.transform : null;
    }

    private void OnDisable()
    {
        if (_animator != null)
        {
            _animator.speed = 1f;
        }
    }

    private void Update()
    {
        UpdateSpasm();
    }

    // Animator가 포즈를 만든 뒤에 비율과 머리·눈·아가리를 덧입힌다.
    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;

        _headBone.localScale = Vector3.one * _headBoneScale;
        _leftLowerArm.localPosition = _leftLowerArmRest * _armStretch;
        _leftHand.localPosition = _leftHandRest * _armStretch;
        _rightLowerArm.localPosition = _rightLowerArmRest * _armStretch;
        _rightHand.localPosition = _rightHandRest * _armStretch;

        UpdateHead(deltaTime);
        UpdateMaw(deltaTime);
        UpdateEyes(deltaTime);
    }

    // 정상 → (멈춤 또는 튐) → 정상 … 을 불규칙하게 반복한다.
    private void UpdateSpasm()
    {
        _spasmTimer -= Time.deltaTime;
        if (_spasmTimer > 0f)
        {
            return;
        }

        _inSpasm = !_inSpasm;
        if (_inSpasm)
        {
            _spasmTimer = Random.Range(_spasmTime.x, _spasmTime.y);
            _animator.speed = Random.value < 0.5f ? 0f : _surgeSpeed;

            // 경련할 때마다 머리가 다른 각도로 뚝 꺾인다.
            _headTarget = Quaternion.Euler(
                Random.Range(-_headSnapAngle, _headSnapAngle) * 0.5f,
                Random.Range(-_headSnapAngle, _headSnapAngle),
                Random.Range(-_headSnapAngle, _headSnapAngle) * 1.4f);
        }
        else
        {
            _spasmTimer = Random.Range(_calmTime.x, _calmTime.y);
            _animator.speed = 1f;
        }
    }

    private void UpdateHead(float deltaTime)
    {
        if (_head == null)
        {
            return;
        }

        _headOffset = Quaternion.Slerp(_headOffset, _headTarget, 1f - Mathf.Exp(-_headSnapSharpness * deltaTime));
        _head.localRotation = _headRest * _headOffset;
    }

    private void UpdateMaw(float deltaTime)
    {
        if (_mawLeft == null || _mawRight == null)
        {
            return;
        }

        float breathing = 0.15f + 0.15f * Mathf.Sin(Time.time * 1.3f);
        bool near = _viewer != null && (_viewer.position - transform.position).sqrMagnitude < _mawNearDistance * _mawNearDistance;
        float target = near ? 1f : breathing;
        _mawOpen = Mathf.MoveTowards(_mawOpen, target, deltaTime * 2.5f);

        float angle = _mawOpen * _mawOpenAngle;
        _mawLeft.localRotation = _mawLeftRest * Quaternion.Euler(0f, -angle, 0f);
        _mawRight.localRotation = _mawRightRest * Quaternion.Euler(0f, angle, 0f);
    }

    private void UpdateEyes(float deltaTime)
    {
        if (_eyes == null)
        {
            return;
        }

        float lookBlend = 1f - Mathf.Exp(-_eyeLookSharpness * deltaTime);
        for (int i = 0; i < _eyes.Length; i++)
        {
            Transform eye = _eyes[i];
            Quaternion rest = eye.parent.rotation * _eyeRest[i];

            if (_viewer != null)
            {
                // 눈이 뒤통수 쪽을 보지 않도록 원래 방향에서 일정 각도 안으로만 돌린다.
                Vector3 toViewer = _viewer.position - eye.position;
                Quaternion look = Quaternion.LookRotation(toViewer, rest * Vector3.up);
                float angle = Quaternion.Angle(rest, look);
                Quaternion target = angle > _eyeLookLimit ? Quaternion.Slerp(rest, look, _eyeLookLimit / angle) : look;
                eye.rotation = Quaternion.Slerp(eye.rotation, target, lookBlend);
            }

            // 제각각 깜빡인다.
            _blinkCooldowns[i] -= deltaTime;
            if (_blinkCooldowns[i] <= 0f)
            {
                _blinkTimers[i] = _blinkDuration;
                _blinkCooldowns[i] = Random.Range(1.5f, 6f);
            }

            float open = 1f;
            if (_blinkTimers[i] > 0f)
            {
                _blinkTimers[i] -= deltaTime;
                open = 0.08f;
            }

            Vector3 scale = _eyeScale[i];
            scale.y *= open;
            eye.localScale = scale;
        }
    }
}
