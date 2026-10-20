using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 7f;
    [SerializeField] private float _sprintBonus = 5f;
    [Tooltip("입력 방향으로 최고 속도에 도달하기까지의 가속도")]
    [SerializeField] private float _acceleration = 30f;
    [Tooltip("키를 뗐을 때 멈추기까지의 감속도")]
    [SerializeField] private float _deceleration = 28f;
    [Tooltip("공중에서 가속과 감속에 곱하는 비율 (낮을수록 방향 전환이 어렵다)")]
    [SerializeField, Range(0f, 1f)] private float _airControlMultiplier = 0.35f;

    [SerializeField] private PlayerVerticalMotion _verticalMotion = new PlayerVerticalMotion();

    [Header("Stamina")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _staminaDrainRate = 20f;
    [SerializeField] private float _staminaRecoveryRate = 10f;

    [Header("Components")]
    [SerializeField] private CharacterController _characterController;

    private Vector3 _horizontalVelocity;
    private float _currentStamina;
    private bool _isSprinting;

    private void Awake()
    {
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }

        _currentStamina = _maxStamina;
    }

    private void Update()
    {
        Vector3 direction = ReadMoveDirection();

        HandleSprint(direction);
        HandleStamina();

        UpdateHorizontalVelocity(direction, GetTargetSpeed());

        Vector3 velocity = _horizontalVelocity;
        velocity.y = _verticalMotion.Tick(ReadVerticalInput(), Time.deltaTime);

        _characterController.Move(velocity * Time.deltaTime);
    }

    private Vector3 ReadMoveDirection()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(h, 0f, v);
        direction.Normalize();

        return transform.TransformDirection(direction);
    }

    private VerticalMotionInput ReadVerticalInput()
    {
        bool hitCeiling = (_characterController.collisionFlags & CollisionFlags.Above) != 0;

        return new VerticalMotionInput(
            _characterController.isGrounded,
            hitCeiling,
            Input.GetButtonDown("Jump"),
            Input.GetButtonUp("Jump"));
    }

    private float GetTargetSpeed()
    {
        return _isSprinting ? _moveSpeed + _sprintBonus : _moveSpeed;
    }

    // 목표 속도를 향해 조금씩 가속하고, 입력이 없으면 조금씩 감속한다.
    private void UpdateHorizontalVelocity(Vector3 direction, float targetSpeed)
    {
        bool hasInput = direction.sqrMagnitude > 0f;
        float rate = hasInput ? _acceleration : _deceleration;

        if (!_verticalMotion.HasGroundContact)
        {
            rate *= _airControlMultiplier;
        }

        _horizontalVelocity = Vector3.MoveTowards(
            _horizontalVelocity,
            direction * targetSpeed,
            rate * Time.deltaTime);
    }

    private void HandleSprint(Vector3 direction)
    {
        bool isShiftPressed = Input.GetKey(KeyCode.LeftShift);
        bool isMoving = direction.sqrMagnitude > 0f;
        bool hasStamina = _currentStamina > 0f;

        _isSprinting = isShiftPressed && isMoving && hasStamina;
    }

    private void HandleStamina()
    {
        if (_isSprinting)
        {
            _currentStamina -= _staminaDrainRate * Time.deltaTime;

            if (_currentStamina <= 0f)
            {
                _currentStamina = 0f;
                _isSprinting = false;
            }
        }
        else
        {
            _currentStamina += _staminaRecoveryRate * Time.deltaTime;

            if (_currentStamina > _maxStamina)
            {
                _currentStamina = _maxStamina;
            }
        }
    }

    public float GetStaminaRatio()
    {
        if (_maxStamina <= 0f)
        {
            return 0f;
        }

        return _currentStamina / _maxStamina;
    }
}
