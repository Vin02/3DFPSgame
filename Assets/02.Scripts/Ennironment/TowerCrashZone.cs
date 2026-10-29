using UnityEngine;

// 탑 둘레의 트리거. 좀비가 닿으면 그 좀비는 사라지고 플레이어가 피해를 입는다.
// 좀비는 탑 벽에 막혀 더 가까이 올 수 없으므로, 벽 바로 앞에 이 구역을 둔다.
public class TowerCrashZone : MonoBehaviour
{
    [SerializeField] private PlayerHealth _playerHealth;
    [Tooltip("좀비 한 마리가 부딪힐 때마다 플레이어가 입는 피해")]
    [SerializeField] private float _damagePerZombie = 10f;

    private void Awake()
    {
        if (_playerHealth == null)
        {
            Debug.LogError("TowerCrashZone: PlayerHealth가 연결되지 않았습니다.");
            enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enabled)
        {
            return;
        }

        Zombie zombie = other.GetComponentInParent<Zombie>();
        if (zombie == null || zombie.IsDead)
        {
            return;
        }

        zombie.CrashIntoTower();
        _playerHealth.TakeDamage(_damagePerZombie);
    }
}
