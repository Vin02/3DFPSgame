
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    private const float Gravity = -20f;

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 7f;
    [SerializeField] private float _sprintBonus = 5f;

    [Header("Jump")]
    [SerializeField] private float _jumpPower = 10f;
    [SerializeField] private int _maxJumpCount = 2;

    [Header("Stamina")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _staminaDrainRate = 20f;
    [SerializeField] private float _staminaRecoveryRate = 10f;

    [Header("Components")]
    [SerializeField] private CharacterController _characterController;

    private float _yVelocity;
    private float _currentStamina;
    private int _jumpCount;
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
        // 이동 입력
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(h, 0f, v);
        direction.Normalize();
        direction = transform.TransformDirection(direction);

        // 달리기 판정
        HandleSprint(direction);

        // 스태미나 처리
        HandleStamina();

        // 중력 및 착지 처리
        HandleGravity();

        // 점프 처리
        HandleJump();

        // 최종 이동
        float currentSpeed = _moveSpeed;

        if (_isSprinting)
        {
            currentSpeed += _sprintBonus;
        }

        Vector3 velocity = direction * currentSpeed;
        velocity.y = _yVelocity;

        _characterController.Move(velocity * Time.deltaTime);
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

    private void HandleGravity()
    {
        if (_characterController.isGrounded)
        {
            _yVelocity = -2f;
            _jumpCount = 0;
        }
        else
        {
            _yVelocity += Gravity * Time.deltaTime;
        }
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") &&
            _jumpCount < _maxJumpCount)
        {
            _yVelocity = _jumpPower;
            _jumpCount++;
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
