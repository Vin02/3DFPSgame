using UnityEngine;

// 공중에 떠 있는 동안 날개를 파닥이고, 땅에 닿으면 날개를 접는다.
[RequireComponent(typeof(CharacterController))]
public class WingFlapper : MonoBehaviour
{
    private const string LeftWingName = "wingL";
    private const string RightWingName = "wingR";

    [Header("Flap")]
    [SerializeField] private float _flapsPerSecond = 6f;
    [SerializeField] private float _flapAngle = 55f;
    [SerializeField] private float _foldSpeed = 8f;

    [Header("Components")]
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private Transform _leftWing;
    [SerializeField] private Transform _rightWing;

    private FlappingWing _left;
    private FlappingWing _right;
    private float _flapStrength;
    private float _phase;

    private void Awake()
    {
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }

        if (_leftWing == null)
        {
            _leftWing = transform.Find(LeftWingName);
        }

        if (_rightWing == null)
        {
            _rightWing = transform.Find(RightWingName);
        }

        if (_leftWing == null || _rightWing == null)
        {
            GameLogger.LogWarning("날개(wingL, wingR)를 찾지 못해 파닥임을 사용할 수 없습니다.");
            enabled = false;
            return;
        }

        CreateWings();
    }

    private void CreateWings()
    {
        _left = new FlappingWing(_leftWing);
        _right = new FlappingWing(_rightWing);
    }

    private void Update()
    {
        // Play 중 스크립트가 재컴파일되면 직렬화되지 않는 필드가 비므로 다시 만든다.
        if (_left == null || _right == null)
        {
            CreateWings();
        }

        UpdateFlapStrength();
        UpdatePhase();

        // 0(접힘) ~ 1(활짝 펼침). 위로 퍼덕일 때만 움직이도록 코사인을 0~1로 만든다.
        float openness = (1f - Mathf.Cos(_phase)) * 0.5f * _flapStrength;
        float angle = openness * _flapAngle;

        _left.Flap(angle);
        _right.Flap(angle);
    }

    private void UpdateFlapStrength()
    {
        float targetStrength = _characterController.isGrounded ? 0f : 1f;
        _flapStrength = Mathf.MoveTowards(_flapStrength, targetStrength, _foldSpeed * Time.deltaTime);
    }

    private void UpdatePhase()
    {
        if (_flapStrength <= 0f)
        {
            _phase = 0f;
            return;
        }

        _phase += 2f * Mathf.PI * _flapsPerSecond * Time.deltaTime;
    }

    // 날개 하나의 기본 자세를 기억하고, 어깨(날개 윗부분)를 축으로 바깥쪽으로 벌린다.
    private sealed class FlappingWing
    {
        private readonly Transform _wing;
        private readonly Vector3 _restPosition;
        private readonly Quaternion _restRotation;
        private readonly Vector3 _pivot;
        private readonly float _outwardSign;

        public FlappingWing(Transform wing)
        {
            _wing = wing;
            _restPosition = wing.localPosition;
            _restRotation = wing.localRotation;
            _pivot = _restPosition + _restRotation * new Vector3(0f, wing.localScale.y * 0.5f, 0f);
            _outwardSign = Mathf.Sign(_restPosition.x);
        }

        public void Flap(float angle)
        {
            Quaternion delta = Quaternion.AngleAxis(_outwardSign * angle, Vector3.forward);

            _wing.localPosition = _pivot + delta * (_restPosition - _pivot);
            _wing.localRotation = delta * _restRotation;
        }
    }
}
