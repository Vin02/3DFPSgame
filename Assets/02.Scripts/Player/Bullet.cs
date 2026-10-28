using UnityEngine;

// 총구에서 날아가는 총알. 매 프레임 이동할 구간을 레이캐스트로 검사하므로 빨라도 얇은 물체를 뚫고 지나가지 않는다.
// 맞으면 피해와 피격 이펙트를 내고 풀로 돌아간다. 아무것도 맞히지 못하면 사거리 끝에서 사라진다.
public class Bullet : MonoBehaviour
{
    [SerializeField] private float _speed = 140f;
    [SerializeField] private float _maxDistance = 120f;
    [SerializeField] private TrailRenderer _trail;

    private Vector3 _direction;
    private float _traveled;
    private float _damage;
    private LayerMask _hitMask;
    private EffectPool _hitEffectPool;
    private EffectPool _bloodEffectPool;
    private PooledObject _pooled;
    private bool _flying;

    public void Fire(Vector3 origin, Vector3 direction, float damage, LayerMask hitMask, EffectPool hitEffectPool, EffectPool bloodEffectPool)
    {
        // 풀이 생성 직후에 PooledObject를 붙일 수 있어서, Awake가 아니라 처음 쏠 때 찾는다.
        if (_pooled == null)
        {
            _pooled = GetComponent<PooledObject>();
        }

        transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
        _direction = direction;
        _traveled = 0f;
        _damage = damage;
        _hitMask = hitMask;
        _hitEffectPool = hitEffectPool;
        _bloodEffectPool = bloodEffectPool;
        _flying = true;

        // 풀에서 다시 꺼냈을 때 이전 위치에서 꼬리가 이어지지 않도록 지운다.
        if (_trail != null)
        {
            _trail.Clear();
        }
    }

    private void Update()
    {
        if (!_flying)
        {
            return;
        }

        float step = _speed * Time.deltaTime;
        // 좀비의 머리 살덩이처럼 몸 콜라이더 밖에 있는 부위는 트리거로 맞힌다. 좀비가 아닌 트리거는 지나친다.
        if (Physics.Raycast(transform.position, _direction, out RaycastHit hit, step, _hitMask, QueryTriggerInteraction.Collide)
            && (!hit.collider.isTrigger || hit.collider.GetComponentInParent<Zombie>() != null))
        {
            transform.position = hit.point;
            OnHit(hit);
            return;
        }

        transform.position += _direction * step;
        _traveled += step;
        if (_traveled >= _maxDistance)
        {
            Finish();
        }
    }

    private void OnHit(RaycastHit hit)
    {
        // 유니티 오브젝트는 ?. 대신 != null로 검사한다. 콜라이더가 자식에 있어도 맞도록 부모에서 찾는다.
        Zombie zombie = hit.collider.GetComponentInParent<Zombie>();

        // 좀비를 맞히면 피, 그 외에는 기본 피격 이펙트를 낸다.
        EffectPool effectPool = zombie != null && _bloodEffectPool != null ? _bloodEffectPool : _hitEffectPool;
        if (effectPool != null)
        {
            effectPool.Spawn(hit.point, Quaternion.LookRotation(hit.normal));
        }

        if (zombie != null)
        {
            zombie.TakeDamage(_damage);
        }

        Drum drum = hit.collider.GetComponentInParent<Drum>();
        if (drum != null)
        {
            drum.Damage(_damage);
        }

        Finish();
    }

    // 풀에서 꺼낸 것이면 풀로 돌려보내고, 아니면 제거한다.
    private void Finish()
    {
        _flying = false;

        if (_pooled != null)
        {
            _pooled.Release();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
