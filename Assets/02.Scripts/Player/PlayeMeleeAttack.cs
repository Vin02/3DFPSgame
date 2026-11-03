using System.Collections.Generic;
using UnityEngine;

public enum MeleeHitMode
{
    Collider,
    Fan
}

// 근접(Melee) 모드에서 마우스 왼쪽 버튼을 누르면 칼을 휘두른다.
// Animator의 Slash 트리거로 Melee 레이어의 베기 모션을 재생하고, 그 모션이 휘두르는 구간(정규화 시간)에만 칼날 콜라이더를 켜서 닿은 적에게 피해를 준다.
// 칼의 충돌은 SwordHit이 감지해서 Hit으로 넘긴다. 같은 베기에서 한 대상은 한 번만 맞힌다.
public class PlayeMeleeAttack : MonoBehaviour
{
    private static readonly int SlashHash = Animator.StringToHash("Slash");

    [SerializeField] private Animator _animator;
    [Tooltip("오른손에 든 칼. 비어 있으면 Animator의 오른손 본 아래에서 'Sword'를 찾는다.")]
    [SerializeField] private SwordHit _sword;
    [Header("Stat")]
    [SerializeField] private float _damage = 120f;
    [SerializeField] private float _attackInterval = 0.5f;
    [Header("Animation")]
    [SerializeField] private string _meleeLayerName = "Melee";
    [SerializeField] private string _slashStateName = "Slash";
    [Tooltip("베기 클립의 정규화 시간(0~1) 중 칼날 콜라이더가 켜지는 시점. 모션이 휘두르기 시작하는 순간에 맞춘다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _hitStart = 0.2f;
    [Tooltip("칼날 콜라이더가 꺼지는 시점(정규화 시간). 휘두르기가 끝나는 순간에 맞춘다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _hitEnd = 0.6f;
    [SerializeField] private MeleeHitMode _meleeHitMode = MeleeHitMode.Collider;

    [Header("부채꼴")]
    [SerializeField] private float _range = 2f;
    [SerializeField] private float _angle = 90f;
    [SerializeField] private LayerMask _targetMask;

    private Player _player;
    private float _attackTimer;
    private int _meleeLayer;
    private bool _isHitting;
    private readonly List<Component> _hitTargets = new List<Component>();
    private readonly Collider[] _colliders = new Collider[32];

    private void Awake()
    {
        _player = GetComponent<Player>();
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        if (_player == null || _animator == null)
        {
            GameLogger.LogError("PlayeMeleeAttack: Player 또는 Animator를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        _meleeLayer = _animator.GetLayerIndex(_meleeLayerName);
        if (_meleeLayer < 0)
        {
            GameLogger.LogError($"PlayeMeleeAttack: Animator에 '{_meleeLayerName}' 레이어가 없습니다.");
            enabled = false;
            return;
        }

        if (_sword == null)
        {
            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform sword = hand != null ? hand.Find("Sword") : null;
            if (sword != null && !sword.TryGetComponent(out _sword))
            {
                _sword = sword.gameObject.AddComponent<SwordHit>();
            }
        }

        if (_sword == null)
        {
            GameLogger.LogWarning("PlayeMeleeAttack: 칼(Sword)을 찾지 못해 근접 공격이 적을 맞히지 못합니다.");
        }
    }

    private void OnDisable()
    {
        SetHitting(false);
    }

    private void Update()
    {
        if (GameManager.IsPaused)
        {
            return;
        }

        _attackTimer -= Time.deltaTime;

        if (_player.Mode != WeaponMode.Melee)
        {
            SetHitting(false);
            return;
        }

        if (Input.GetButtonDown("Fire1") && _attackTimer <= 0f)
        {
            _attackTimer = _attackInterval;
            _animator.SetTrigger(SlashHash);
        }

        // 베기 모션이 휘두르는 구간에 있는 동안만 칼날 콜라이더를 켠다. 전환 중에는 아직 모션이 섞이는 중이라 켜지 않는다.
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(_meleeLayer);
        bool inWindow = state.IsName(_slashStateName)
            && !_animator.IsInTransition(_meleeLayer)
            && state.normalizedTime >= _hitStart
            && state.normalizedTime < _hitEnd;
        SetHitting(inWindow);
    }

    // 부채꼴 모드: 플레이어 앞쪽 _range 안, 정면 기준 좌우 _angle/2 이내의 대상에게 피해를 준다. 판정 구간이 열리는 순간 한 번 부른다.
    public void FanAttack()
    {
        if (_meleeHitMode != MeleeHitMode.Fan) return;

        Vector3 origin = transform.position;
        int count = Physics.OverlapSphereNonAlloc(origin, _range, _colliders, _targetMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            // 덩치 큰 대상도 맞도록 중심이 아니라 콜라이더에서 가장 가까운 점으로 각도를 잰다.
            Vector3 point = _colliders[i].ClosestPoint(origin);
            Vector3 direction = point - origin;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, direction) > _angle * 0.5f)
            {
                continue;
            }

            Hit(_colliders[i], point);
        }
    }

    // 판정 구간이 새로 열릴 때마다 맞힌 대상을 비우고, 구간이 닫히면 칼날 콜라이더를 끈다.
    private void SetHitting(bool hitting)
    {
        if (_isHitting == hitting)
        {
            return;
        }

        _isHitting = hitting;
        if (hitting)
        {
            _hitTargets.Clear();
            FanAttack();
        }

        if (_sword != null && _meleeHitMode == MeleeHitMode.Collider)
        {
            _sword.SetColliderEnabled(hitting);
        }
    }

    // 칼이 other에 닿아 있는 동안 SwordHit이 부른다. 공격 판정 구간이 아니거나 이미 맞힌 대상이면 무시한다.
    public void Hit(Collider other, Vector3 point)
    {
        if (!_isHitting)
        {
            return;
        }

        // 콜라이더가 자식에 있어도 맞도록 부모에서 찾는다. 한 대상에 콜라이더가 여럿일 수 있어 이미 맞힌 대상은 건너뛴다.
        Zombie zombie = other.GetComponentInParent<Zombie>();
        if (zombie != null && !_hitTargets.Contains(zombie))
        {
            _hitTargets.Add(zombie);
            zombie.TakeDamage(_damage);
        }

        CosmicColossus colossus = other.GetComponentInParent<CosmicColossus>();
        if (colossus != null && !_hitTargets.Contains(colossus))
        {
            _hitTargets.Add(colossus);
            colossus.TakeDamage(_damage, point);
        }

        BlackHole blackHole = other.GetComponentInParent<BlackHole>();
        if (blackHole != null && !_hitTargets.Contains(blackHole))
        {
            _hitTargets.Add(blackHole);
            blackHole.Damage(_damage);
        }

        Drum drum = other.GetComponentInParent<Drum>();
        if (drum != null && !_hitTargets.Contains(drum))
        {
            _hitTargets.Add(drum);
            drum.Damage(_damage);
        }
    }
}
