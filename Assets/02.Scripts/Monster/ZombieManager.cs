using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

// 좀비의 생성과 수를 관리한다. 일정 간격으로 스포너 중 한 곳에서 좀비를 꺼내고, 동시에 살아 있는 수를 제한한다.
// 끝없이 생성되며, 지금 살아 있는 수(AliveCount)와 처치한 수(KillCount)를 알려준다.
// 좀비는 오브젝트 풀에서 꺼내 쓰고, 죽어서 일정 시간이 지나면 풀로 돌려보낸다.
public class ZombieManager : MonoBehaviour
{
    [Header("생성")]
    [SerializeField] private Zombie[] _zombiePrefabs;
    [SerializeField] private ZombieSpawner[] _spawners;
    [Tooltip("생성된 좀비의 미니맵 아이콘이 따라갈 미니맵 카메라")]
    [SerializeField] private Transform _minimapCamera;
    [Tooltip("생성된 좀비를 담을 부모 (비워두면 씬 루트)")]
    [SerializeField] private Transform _container;
    [Tooltip("이 거리 안의 스포너에서는 생성하지 않는다. (눈앞에서 갑자기 나타나지 않도록)")]
    [SerializeField] private Transform _player;
    [SerializeField] private float _minSpawnDistance = 15f;

    [Header("규칙")]
    [SerializeField] private int _initialCount = 2;
    [SerializeField] private int _maxAlive = 6;
    [SerializeField] private float _spawnInterval = 5f;

    [Header("씬에 미리 놓은 좀비")]
    [SerializeField] private Zombie[] _existingZombies;

    private readonly List<Zombie> _alive = new List<Zombie>();
    private ObjectPool<Zombie> _pool;
    private float _spawnTimer;
    private int _killCount;

    public int AliveCount => _alive.Count;
    public int KillCount => _killCount;

    private void Awake()
    {
        if (_zombiePrefabs == null || _zombiePrefabs.Length == 0 || _spawners == null || _spawners.Length == 0)
        {
            GameLogger.LogError("ZombieManager에 좀비 프리팹 또는 스포너가 없습니다.");
            enabled = false;
            return;
        }

        _pool = new ObjectPool<Zombie>(CreateZombie, OnGetZombie, OnReleaseZombie, OnDestroyZombie, true, _maxAlive, _maxAlive * 2);

        foreach (Zombie zombie in _existingZombies)
        {
            if (zombie != null)
            {
                Track(zombie);
                _alive.Add(zombie);
            }
        }
    }

    private void Start()
    {
        while (_alive.Count < _initialCount && _alive.Count < _maxAlive)
        {
            Spawn();
        }
    }

    private void Update()
    {
        _spawnTimer += Time.deltaTime;
        if (_spawnTimer < _spawnInterval)
        {
            return;
        }

        _spawnTimer = 0f;

        if (_alive.Count < _maxAlive)
        {
            Spawn();
        }
    }

    private void Spawn()
    {
        ZombieSpawner spawner = PickSpawner();
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Zombie zombie = _pool.Get();
        zombie.ResetForSpawn(spawner.GetSpawnPoint(), rotation);

        MinimapIcon icon = zombie.GetComponentInChildren<MinimapIcon>(true);
        if (icon != null)
        {
            icon.Initialize(_minimapCamera);
        }

        _alive.Add(zombie);
    }

    // 플레이어에게서 충분히 먼 스포너 중 하나를 고른다. 모두 가까우면 아무 스포너나 쓴다.
    private ZombieSpawner PickSpawner()
    {
        int start = Random.Range(0, _spawners.Length);
        if (_player == null)
        {
            return _spawners[start];
        }

        float minSqr = _minSpawnDistance * _minSpawnDistance;
        for (int i = 0; i < _spawners.Length; i++)
        {
            ZombieSpawner spawner = _spawners[(start + i) % _spawners.Length];
            if ((spawner.transform.position - _player.position).sqrMagnitude >= minSqr)
            {
                return spawner;
            }
        }

        return _spawners[start];
    }

    // 좀비가 죽거나 풀로 돌아갈 때를 알 수 있도록 이벤트를 연결한다. (풀에서 만든 좀비는 만들 때 한 번만 연결)
    private void Track(Zombie zombie)
    {
        zombie.Died += OnZombieDied;
        zombie.Despawn += OnZombieDespawn;
    }

    private void OnZombieDied(Zombie zombie)
    {
        _alive.Remove(zombie);
        _killCount++;
    }

    private void OnZombieDespawn(Zombie zombie)
    {
        _pool.Release(zombie);
    }

    private Zombie CreateZombie()
    {
        Zombie zombie = Instantiate(_zombiePrefabs[Random.Range(0, _zombiePrefabs.Length)], _container);
        Track(zombie);
        zombie.gameObject.SetActive(false);
        return zombie;
    }

    private static void OnGetZombie(Zombie zombie)
    {
        zombie.gameObject.SetActive(true);
    }

    private static void OnReleaseZombie(Zombie zombie)
    {
        zombie.gameObject.SetActive(false);
    }

    private static void OnDestroyZombie(Zombie zombie)
    {
        Destroy(zombie.gameObject);
    }
}
