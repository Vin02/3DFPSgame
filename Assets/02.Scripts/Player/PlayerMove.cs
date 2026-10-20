using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private const float Gravity = -20f;
    private const int MaxJumps = 2;
    private const float MaxStamina = 100f;
    private float _yVelocity = 0f;
    private int _jumpCount = 0;
    private float _stamina = 100f;
    [SerializeField] private float _moveSpeed = 7f;
    [SerializeField] private float _sprintSpeed = 5f;
    [SerializeField] private float _jumpPower = 10f;
    [SerializeField] private CharacterController _characterController;

    private void Awake()
    {
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }
    }

    private void Update()
    {
        // 키보드 입력을 받고
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        // 해당 입력으로 방향을 구해서


        Vector3 direction = new Vector3(h, 0, v);
        direction.Normalize(); // 대각선이어도 방향 크기를 1로 바꾸기 위해 정규화
        direction = transform.TransformDirection(direction);
        // 로컬 좌표계 : 한 대상을 중심으로
        // 월드 좌표계 : 세상을 중심으로 
        // 중력 누적
        if (_characterController.isGrounded == false)
        {
            _yVelocity += Gravity * Time.deltaTime;
        }

        else
        {
            _yVelocity = -1f;
            _jumpCount = 0;
        }

        // 만약 점프 키를 누르면 점프한다
        if (Input.GetButtonDown("Jump") && _jumpCount < MaxJumps)
        {
            _yVelocity = _jumpPower;
            _jumpCount++;
        }

        // 속도를 구하기

        float currentSpeed = Input.GetButton("Sprint") ? _moveSpeed + _sprintSpeed : _moveSpeed;
        Vector3 velocity = direction * currentSpeed;
        velocity.y = _yVelocity;
        // 방향과 속력을 곱해서 이동한다.
        // transform.position += direction * _moveSpeed * Time.deltaTime;
        _characterController.Move(velocity * Time.deltaTime);
    }
}
