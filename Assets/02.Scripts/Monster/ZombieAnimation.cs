using UnityEngine;

// 좀비의 이동 속도와 상태를 Animator 파라미터로 전달한다.
public class ZombieAnimation : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    [SerializeField] private Animator _animator;
    [SerializeField] private Zombie _zombie;
    [SerializeField] private CharacterController _controller;

    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }

        if (_zombie == null)
        {
            _zombie = GetComponentInParent<Zombie>();
        }

        if (_controller == null)
        {
            _controller = GetComponentInParent<CharacterController>();
        }
    }

    // 풀에서 다시 꺼낼 때 죽은 자세가 남지 않도록 애니메이터를 처음 상태로 되돌린다.
    private void OnEnable()
    {
        if (_animator != null && _animator.isInitialized)
        {
            _animator.Rebind();
        }
    }

    private void Update()
    {
        // 죽어서 캐릭터 컨트롤러가 꺼져 있으면 속도는 0으로 본다.
        Vector3 velocity = _controller.enabled ? _controller.velocity : Vector3.zero;
        velocity.y = 0f;

        _animator.SetFloat(SpeedHash, velocity.magnitude, 0.1f, Time.deltaTime);
        _animator.SetBool(AttackHash, _zombie.State == ZombieState.Attack);
        _animator.SetBool(DeadHash, _zombie.IsDead);
    }
}
