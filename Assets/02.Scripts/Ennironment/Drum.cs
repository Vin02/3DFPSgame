using System.Collections;
using UnityEngine;

// 체력이 0이 되면 위로 튀어오르며 폭발하는 드럼통
[RequireComponent(typeof(Rigidbody))]
public class Drum : MonoBehaviour
{
    [SerializeField] private ParticleSystem _explosionEffectPrefab;

    [Header("스탯")]
    [SerializeField] private float _health = 30f;
    [SerializeField] private float _launchPower = 20f;
    [SerializeField] private float _spinPower = 5f;
    [SerializeField] private float _destoryDelay = 8f;
    [SerializeField] private float _explosionRange = 4.5f;
    [SerializeField] private float _damage = 100f;
    [SerializeField] private float _explosionDelay = 0.1f;

    private Rigidbody _rigidbody;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    public void Damage(float damage)
    {
        if (_health <= 0)
        {
            return;
        }

        _health -= damage;

        if (_health <= 0)
        {
            StartCoroutine(Explode_Coroutine());
        }
    }

    private IEnumerator Explode_Coroutine()
    {
        yield return new WaitForSeconds(_explosionDelay);
        // 1. 폭발 이펙트를 생성한다.
        ParticleSystem effect = Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
        effect.Play();

        // 2. 위로 튀어오르며 빙글빙글 돈다.
        _rigidbody.AddForce(Vector3.up * _launchPower, ForceMode.Impulse);
        _rigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * _spinPower, ForceMode.Impulse);

        ApplyAreaDamage();

        // 3. 딜레이 후 사라진다.
        yield return new WaitForSeconds(_destoryDelay);
        Destroy(gameObject);
    }

    private void ApplyAreaDamage()
    {
        // 주위에 있는 모든 드럼통 찾기
        Collider[] colliders = Physics.OverlapSphere(transform.position, _explosionRange, LayerMask.GetMask("Drum", "Zombie"));


        // 드럼통들에게 데미지를 준다.
        foreach (Collider col in colliders)
        {
            col.GetComponent<Drum>()?.Damage(_damage);
            col.GetComponent<Zombie>()?.TakeDamage(_damage);
        }
    }
}