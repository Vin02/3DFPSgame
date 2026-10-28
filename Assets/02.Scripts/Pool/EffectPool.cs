using UnityEngine;
using UnityEngine.Pool;

// 한 종류의 이펙트 프리팹을 미리 만들어 두고 돌려 쓴다. 필요하면 풀 크기까지 늘어난다.
public class EffectPool : MonoBehaviour
{
    [SerializeField] private GameObject _prefab;
    [SerializeField] private int _defaultCapacity = 10;
    [SerializeField] private int _maxSize = 50;
    [Tooltip("꺼낸 뒤 이 시간(초)이 지나면 자동으로 풀로 돌아간다. 0이면 사용하는 쪽에서 직접 반환한다.")]
    [SerializeField] private float _lifetime = 2f;

    private ObjectPool<PooledObject> _pool;

    private void Awake()
    {
        if (_prefab == null)
        {
            GameLogger.LogError($"{name}: EffectPool에 프리팹이 없습니다.");
            enabled = false;
            return;
        }

        _pool = new ObjectPool<PooledObject>(
            CreateInstance, null, OnRelease, OnDestroyInstance, true, _defaultCapacity, _maxSize);
    }

    public PooledObject Spawn(Vector3 position, Quaternion rotation)
    {
        // 풀에 들어 있는 동안 외부에서 파괴된 인스턴스는 버리고 다음 것을 꺼낸다.
        PooledObject instance = _pool.Get();
        while (instance == null)
        {
            instance = _pool.Get();
        }

        instance.Activate(position, rotation, _lifetime);
        return instance;
    }

    private PooledObject CreateInstance()
    {
        GameObject instance = Instantiate(_prefab, transform);

        PooledObject pooled = instance.GetComponent<PooledObject>();
        if (pooled == null)
        {
            pooled = instance.AddComponent<PooledObject>();
        }

        pooled.Initialize(_pool);
        instance.SetActive(false);
        return pooled;
    }

    private static void OnRelease(PooledObject instance)
    {
        instance.gameObject.SetActive(false);
    }

    private static void OnDestroyInstance(PooledObject instance)
    {
        Destroy(instance.gameObject);
    }
}
