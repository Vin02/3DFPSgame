using System;
using System.Collections;
using System.Diagnostics;
using Unity.VisualScripting;
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
    [Header("스탯")]
    [SerializeField] private float _detectDistance = 8f;
    [SerializeField] private float _comebackDistance = 8f;
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private float _attackInterval = 1.2f;
    [SerializeField] private float _moveSpeed = 5f;

    [Header("순찰")]
    [SerializeField] private Transform[] _patrolPoints;     // 비워두면 시작 위치 주변을 랜덤하게 돌아다닌다.
    [SerializeField] private float _patrolRadius = 6f;      // 랜덤 순찰 반경
    [SerializeField] private float _patrolSpeed = 2f;
    [SerializeField] private float _patrolTimeout = 6f;     // 장애물에 걸려도 이 시간이 지나면 순찰을 끝낸다.
    [SerializeField] private float _idleTime = 2f;          // Idle 상태에서 이 시간만큼 쉬고 순찰을 시작
    [SerializeField] private float _patrolArriveDistance = 0.5f;

    private float _attackTimer = 0f;
    private float _idleTimer = 0f;
    private float _patrolTimer = 0f;
    private int _patrolIndex = 0;
    private Vector3 _patrolTarget;
    private CharacterController _controller;
    private GameObject _player;
    private Vector3 _startPosition;
    private ZombieState _state = ZombieState.Idle;

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _startPosition = transform.position;
        _player = GameObject.FindGameObjectWithTag("Player");
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
        StartCoroutine(Hit_Coroutine());
    }

    private IEnumerator Hit_Coroutine()
    {
        yield return new WaitForSeconds(0.3f); // 잠깐 멈칫한다!

        // 맞았으니 쫓아온다.
        _state = ZombieState.Trace;
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

        if (Vector3.Distance(transform.position, _player.transform.position) <= _attackRange)
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

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _attackInterval)
        {
            _attackTimer = 0f;
        }

        float distacne = Vector3.Distance(transform.position, _player.transform.position);
        if (distacne > _attackRange)
        {
            _state = ZombieState.Trace;
        }
    }

    private void Hit()
    {

    }

    private void Dead()
    {

    }
}
