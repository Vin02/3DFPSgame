using UnityEngine;

// 던져진 계란. 무언가에 부딪히면 폭발 이펙트를 내고 주변에 피해를 준 뒤 사라진다.
public class EggProjectile : MonoBehaviour
{
    private GameObject _explosionPrefab;
    private float _damage;
    private float _range;
    private bool _exploded;

    public void Init(GameObject explosionPrefab, float damage, float range)
    {
        _explosionPrefab = explosionPrefab;
        _damage = damage;
        _range = range;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_exploded)
        {
            return;
        }

        _exploded = true;
        Explode();
        Destroy(gameObject);
    }

    private void Explode()
    {
        if (_explosionPrefab != null)
        {
            Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, _range, LayerMask.GetMask("Drum", "Zombie"));

        foreach (Collider col in colliders)
        {
            Drum drum = col.GetComponentInParent<Drum>();
            if (drum != null)
            {
                drum.Damage(_damage);
            }

            Zombie zombie = col.GetComponentInParent<Zombie>();
            if (zombie != null)
            {
                zombie.TakeDamage(_damage);
            }
        }
    }
}
