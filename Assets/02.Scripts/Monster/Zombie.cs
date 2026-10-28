using System;
using System.Collections;
using UnityEngine;

// 유한 상태 머신(FSM) : 유한개의 상태가 있고, 상태마다 행동을 다르게 함으로써 AI를 구현하는 유명한 방법
// 상태 = 객체가 가질 수 있는 어떤 조건이나 상황
// 전이 = 조건에 따라 상태를 변경하는 것
// 머신 = 위 방식에 따라 규칙을 세워둔 것

// 좀비가 가질 수 있는 상태
public enum ZombieState
{
    Idle,
    Patrol,
    Trace,
    Comeback,
    Attack,
    Hit,
    Dead
}
public class Zombie : MonoBehaviour
{
    // 공격 중에 이 거리만큼 더 멀어져야 추적으로 돌아간다. 사거리 경계에서 상태가 계속 바뀌는 것을 막는다.
    private const float AttackExitMargin = 0.3f;

    [Header("스탯")]
    [SerializeField] private float _maxHealth = 30f;
    [SerializeField] private float _detectDistance = 8f;
    [SerializeField] private float _comebackDistance = 8f;
    [Tooltip("몸(캐릭터 컨트롤러) 표면 사이의 거리. 몸이 서로 막기 때문에 중심 거리 기준이면 닿지 않는다.")]
    [SerializeField] private float _attackRange = 1f;
    [SerializeField] private float _attackInterval = 1.2f;
    [SerializeField] private float _attackDamage = 10f;
    [SerializeField] private float _moveSpeed = 5f;
    [Tooltip("죽은 뒤 이 시간(초)이 지나면 풀로 돌아간다.")]
    [SerializeField] private float _despawnDelay = 3f;

    [Header("순찰")]
    [SerializeField] private Transform[] _patrolPoints;     // 비워두면 시작 위치 주변을 랜덤하게 돌아다닌다.
    [SerializeField] private float _patrolRadius = 6f;      // 랜덤 순찰 반경
    [SerializeField] private float _patrolSpeed = 2f;
    [SerializeField] private float _patrolTimeout = 6f;     // 장애물에 걸려도 이 시간이 지나면 순찰을 끝낸다.
    [SerializeField] private float _idleTime = 2f;          // Idle 상태에서 이 시간만큼 쉬고 순찰을 시작
    [SerializeField] private float _patrolArriveDistance = 0.5f;

    private float _health;
    private float _attackTimer = 0f;
    private float _idleTimer = 0f;
    private float _patrolTimer = 0f;
    private float _deadTimer = 0f;
    private bool _despawnRequested;
    private int _patrolIndex = 0;
    private Vector3 _patrolTarget;
    private CharacterController _controller;
    private GameObject _player;
    private PlayerHealth _playerHealth;
    private float _playerRadius;
    private GameObject _minimapIcon;
    private Vector3 _startPosition;
    private ZombieState _state = ZombieState.Idle;

    public ZombieState State => _state;
    public bool IsDead => _state == ZombieState.Dead;

    // 체력이 0이 된 순간
    public event Action<Zombie> Died;
    // 죽은 채로 _despawnDelay가 지나 풀로 돌려보낼 때가 된 순간
    public event Action<Zombie> Despawn;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _startPosition = transform.position;
        _health = _maxHealth;

        MinimapIcon icon = GetComponentInChildren<MinimapIcon>(true);
        _minimapIcon = icon != null ? icon.gameObject : null;

        _player = GameObject.FindGameObjectWithTag("Player");
        _playerHealth = _player.GetComponent<PlayerHealth>();
        _playerRadius = _player.GetComponent<CharacterController>().radius;
    }

    // 풀에서 꺼낸 좀비를 처음 상태로 되돌려 지정한 위치에 세운다.
    public void ResetForSpawn(Vector3 position, Quaternion rotation)
    {
        // 캐릭터 컨트롤러가 켜진 채로 위치를 바꾸면 이전 위치로 되돌아가므로 잠시 끈다.
        _controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        _controller.enabled = true;

        _startPosition = position;
        _health = _maxHealth;
        _state = ZombieState.Idle;
        _attackTimer = 0f;
        _idleTimer = 0f;
        _patrolTimer = 0f;
        _deadTimer = 0f;
        _despawnRequested = false;

        if (_minimapIcon != null)
        {
            _minimapIcon.SetActive(true);
        }
    }

    private void Update()
    {
        switch (_state)
        {
            case ZombieState.Idle:
                Idle();
                break;
            case ZombieState.Patrol:
                Patrol();
                break;
            case ZombieState.Trace:
                Trace();
                break;
            case ZombieState.Comeback:
                Comeback();
                break;
            case ZombieState.Attack:
                Attack();
                break;
            case ZombieState.Hit:
                Hit();
                break;
            case ZombieState.Dead:
                Dead();
                break;

        }
    }

    public void TakeDamage(float damage)
    {
        if (IsDead)
        {
            return;
        }

        _health -= damage;
        if (_health <= 0f)
        {
            Die();
            return;
        }

        StartCoroutine(Hit_Coroutine());
    }

    private void Die()
    {
        _state = ZombieState.Dead;
        _deadTimer = 0f;

        // 죽은 몸은 총알과 이동을 막지 않도록 충돌을 끈다.
        _controller.enabled = false;

        if (_minimapIcon != null)
        {
            _minimapIcon.SetActive(false);
        }

        Died?.Invoke(this);
    }

    private IEnumerator Hit_Coroutine()
    {
        yield return new WaitForSeconds(0.3f); // 잠깐 멈칫한다!

        // 맞았으니 쫓아온다. (그 사이 죽었으면 그대로 둔다)
        if (!IsDead)
        {
            _state = ZombieState.Trace;
        }
    }
    private void Idle()
    {
        float distacne = Vector3.Distance(transform.position, _player.transform.position);
        if (distacne <= _detectDistance)
        {
            _idleTimer = 0f;
            _state = ZombieState.Trace;
            return;
        }

        _idleTimer += Time.deltaTime;
        if (_idleTimer >= _idleTime)
        {
            _idleTimer = 0f;
            _patrolTimer = 0f;
            _patrolTarget = PickPatrolTarget();
            _state = ZombieState.Patrol;
        }
    }

    private Vector3 PickPatrolTarget()
    {
        if (_patrolPoints != null && _patrolPoints.Length > 0)
        {
            Vector3 point = _patrolPoints[_patrolIndex].position;
            _patrolIndex = (_patrolIndex + 1) % _patrolPoints.Length;
            return point;
        }

        Vector2 offset = UnityEngine.Random.insideUnitCircle * _patrolRadius;
        return _startPosition + new Vector3(offset.x, 0f, offset.y);
    }

    private void Patrol()
    {
        // 순찰 중에 플레이어를 발견하면 쫓아간다.
        if (Vector3.Distance(transform.position, _player.transform.position) <= _detectDistance)
        {
            _state = ZombieState.Trace;
            return;
        }

        Vector3 direction = _patrolTarget - transform.position;
        direction.y = 0f;

        // 목표에 도착했거나 너무 오래 걸리면 잠깐 쉰다.
        _patrolTimer += Time.deltaTime;
        if (direction.magnitude <= _patrolArriveDistance || _patrolTimer >= _patrolTimeout)
        {
            _state = ZombieState.Idle;
            return;
        }

        direction.Normalize();

        transform.forward = direction;
        _controller.SimpleMove(direction * _patrolSpeed);
    }

    private void Trace()
    {
        if (Vector3.Distance(transform.position, _player.transform.position) > _comebackDistance)
        {
            _state = ZombieState.Comeback;
        }
        Vector3 direction = _player.transform.position - transform.position;
        direction.y = 0f;
        direction.Normalize();

        transform.forward = direction;
        _controller.SimpleMove(direction * _moveSpeed);

        if (IsInAttackRange(0f))
        {
            _attackTimer = 0f;
            _state = ZombieState.Attack;
        }
    }

    private void Comeback()
    {
        if (Vector3.Distance(transform.position, _startPosition) <= 1f)
        {
            _state = ZombieState.Idle;
        }
        Vector3 direction = _startPosition - transform.position;
        direction.y = 0f;
        direction.Normalize();

        transform.forward = direction;
        _controller.SimpleMove(direction * _moveSpeed);
    }

    private void Attack()
    {
        // 공격 중에는 플레이어를 바라본다.
        Vector3 direction = _player.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.forward = direction.normalized;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _attackInterval)
        {
            _attackTimer = 0f;

            if (_playerHealth != null)
            {
                _playerHealth.TakeDamage(_attackDamage);
            }
        }

        if (!IsInAttackRange(AttackExitMargin))
        {
            _state = ZombieState.Trace;
        }
    }

    private void Hit()
    {

    }

    private void Dead()
    {
        if (_despawnRequested)
        {
            return;
        }

        _deadTimer += Time.deltaTime;
        if (_deadTimer >= _despawnDelay)
        {
            _despawnRequested = true;
            Despawn?.Invoke(this);
        }
    }

    // 높이는 무시하고, 두 캐릭터 컨트롤러 표면 사이의 거리가 사거리 안인지 확인한다.
    private bool IsInAttackRange(float margin)
    {
        Vector3 toPlayer = _player.transform.position - transform.position;
        toPlayer.y = 0f;

        // 캐릭터 컨트롤러 반지름은 로컬 값이라, 크기를 키운 좀비도 맞도록 배율을 곱한다.
        float surfaceDistance = toPlayer.magnitude - _controller.radius * transform.lossyScale.x - _playerRadius;
        return surfaceDistance <= _attackRange + margin;
    }
}
