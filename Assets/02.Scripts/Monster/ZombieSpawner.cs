using UnityEngine;

// 좀비가 생성될 위치. 반경 안의 무작위 지점을 알려주기만 하고, 실제 생성은 ZombieManager가 한다.
public class ZombieSpawner : MonoBehaviour
{
    [SerializeField] private float _radius = 3f;

    public Vector3 GetSpawnPoint()
    {
        Vector2 offset = Random.insideUnitCircle * _radius;
        return transform.position + new Vector3(offset.x, 0f, offset.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
}
