using UnityEngine;
using UnityEngine.Pool;

// 풀에서 꺼내 쓰는 이펙트 오브젝트. 정해진 시간이 지나면 스스로 풀로 돌아가고, 파티클은 꺼낼 때마다 처음부터 재생한다.
public class PooledObject : MonoBehaviour
{
    private IObjectPool<PooledObject> _pool;
    private ParticleSystem[] _particles;
    private float _releaseTime = -1f;

    private void Awake()
    {
        _particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    public void Initialize(IObjectPool<PooledObject> pool)
    {
        _pool = pool;
    }

    // lifetime이 0 이하이면 자동 반환하지 않는다. (직접 Release를 호출해야 한다)
    public void Activate(Vector3 position, Quaternion rotation, float lifetime)
    {
        transform.SetPositionAndRotation(position, rotation);
        _releaseTime = lifetime > 0f ? Time.time + lifetime : -1f;
        gameObject.SetActive(true);

        foreach (ParticleSystem particle in _particles)
        {
            particle.Clear(true);
            particle.Play(true);
        }
    }

    public void Release()
    {
        _releaseTime = -1f;
        _pool.Release(this);
    }

    private void Update()
    {
        if (_releaseTime >= 0f && Time.time >= _releaseTime)
        {
            Release();
        }
    }
}
