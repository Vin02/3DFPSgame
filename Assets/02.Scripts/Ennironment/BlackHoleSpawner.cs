using System.Collections.Generic;
using UnityEngine;

// 일정 간격으로 맵 전역의 무작위 위치에 블랙홀을 만든다. 동시에 존재하는 개수에는 상한이 있다.
public class BlackHoleSpawner : MonoBehaviour
{
    [SerializeField] private BlackHole _prefab;
    [Tooltip("플레이어와 너무 가까운 곳에는 만들지 않는다.")]
    [SerializeField] private Transform _player;

    [Header("생성")]
    [SerializeField] private float _interval = 7f;
    [SerializeField] private int _maxCount = 30;
    [Tooltip("맵 중심에서 이 거리(사각형 반 크기) 안에서 만든다.")]
    [SerializeField] private float _halfExtent = 70f;
    [SerializeField] private float _minPlayerDistance = 15f;
    [Tooltip("땅에서 이만큼 위에 띄운다.")]
    [SerializeField] private float _hoverHeight = 5f;

    private const int PlacementAttempts = 12;

    private readonly List<BlackHole> _alive = new List<BlackHole>();
    private float _timer;

    // 현재 살아 있는 개수 (확인용)
    public int AliveCount => _alive.Count;

    private void Awake()
    {
        if (_prefab == null || _player == null)
        {
            Debug.LogError("BlackHoleSpawner에 프리팹 또는 플레이어가 연결되지 않았습니다. (Tools > Map > Build Black Holes)");
            enabled = false;
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _interval)
        {
            return;
        }

        _timer = 0f;
        Spawn();
    }

    private void Spawn()
    {
        _alive.RemoveAll(IsGone);
        if (_alive.Count >= _maxCount)
        {
            return;
        }

        for (int attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            float x = Random.Range(-_halfExtent, _halfExtent);
            float z = Random.Range(-_halfExtent, _halfExtent);
            Vector3 flat = new Vector3(x, 0f, z);
            Vector3 playerFlat = new Vector3(_player.position.x, 0f, _player.position.z);
            if ((flat - playerFlat).sqrMagnitude < _minPlayerDistance * _minPlayerDistance)
            {
                continue;
            }

            // 땅 높이를 재서 그 위에 띄운다.
            float groundY = 0f;
            if (Physics.Raycast(new Vector3(x, 100f, z), Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
            }

            BlackHole blackHole = Instantiate(_prefab, new Vector3(x, groundY + _hoverHeight, z), Quaternion.identity);
            _alive.Add(blackHole);
            return;
        }
    }

    // 터져서 사라진 블랙홀(Unity가 null로 취급)은 목록에서 뺀다.
    private static bool IsGone(BlackHole blackHole)
    {
        return blackHole == null;
    }
}
