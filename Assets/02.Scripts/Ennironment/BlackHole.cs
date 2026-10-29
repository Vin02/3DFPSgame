using System;
using System.Collections;
using UnityEngine;

// 맵에 떠 있는 블랙홀. 일정 피해 이상 받으면 터지면서 주변의 좀비와 다른 블랙홀에 피해를 준다. (옛 드럼통을 대신한다)
// 가만히 떠서 위아래로 천천히 일렁인다. 소용돌이 모습은 재질(셰이더)이 알아서 움직인다.
public class BlackHole : MonoBehaviour
{
    [Tooltip("폭발 때 나오는 이펙트. 스스로 사라지는 파티클 프리팹")]
    [SerializeField] private GameObject _explosionEffectPrefab;
    [Tooltip("위아래로 일렁이게 할 보이는 부분")]
    [SerializeField] private Transform _visual;

    [Header("스탯")]
    [SerializeField] private float _health = 30f;
    [SerializeField] private float _explosionRange = 28f;
    [SerializeField] private float _damage = 150f;
    [SerializeField] private float _explosionDelay = 0.1f;
    [Tooltip("폭발 이펙트 크기 배율 (블랙홀이 커서 키운다)")]
    [SerializeField] private float _effectScale = 6f;

    [Header("보이는 방향")]
    [Tooltip("소용돌이 원판이 플레이어 쪽을 향하게 한다. (옆에서 보면 선처럼 얇아 보이지 않게)")]
    [SerializeField] private float _faceViewerSharpness = 4f;
    [Tooltip("0 = 완전히 마주 봄, 1 = 수평 그대로")]
    [SerializeField, Range(0f, 1f)] private float _flatness = 0.25f;

    [Header("일렁임")]
    [SerializeField] private float _bobHeight = 0.4f;
    [SerializeField] private float _bobSpeed = 1.2f;

    private Transform _viewer;
    private Vector3 _visualRest;
    private float _phase;
    private bool _exploding;

    // 터질 때 알린다. (스포너가 개수를 센다)
    public event Action<BlackHole> Exploded;

    private void Awake()
    {
        if (_explosionEffectPrefab == null || _visual == null)
        {
            Debug.LogError($"{name}: BlackHole에 필요한 참조가 없습니다.");
            enabled = false;
            return;
        }

        _viewer = Camera.main.transform;
        _visualRest = _visual.localPosition;
        _phase = UnityEngine.Random.value * 10f;
    }

    private void Update()
    {
        _visual.localPosition = _visualRest + Vector3.up * (Mathf.Sin(Time.time * _bobSpeed + _phase) * _bobHeight);

        // 원판의 위쪽(법선)이 플레이어를 향하도록 천천히 돌린다. 수평 쪽으로 조금 기울여 입체감을 준다.
        Vector3 toViewer = (_viewer.position - _visual.position).normalized;
        Vector3 normal = Vector3.Slerp(toViewer, Vector3.up, _flatness).normalized;
        Quaternion target = Quaternion.FromToRotation(Vector3.up, normal);
        _visual.rotation = Quaternion.Slerp(_visual.rotation, target, 1f - Mathf.Exp(-_faceViewerSharpness * Time.deltaTime));
    }

    public void Damage(float damage)
    {
        if (_exploding)
        {
            return;
        }

        _health -= damage;
        if (_health <= 0f)
        {
            _exploding = true;
            StartCoroutine(Explode_Coroutine());
        }
    }

    private IEnumerator Explode_Coroutine()
    {
        yield return new WaitForSeconds(_explosionDelay);

        GameObject effect = Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
        effect.transform.localScale *= _effectScale;

        // 이펙트에 든 PPFXPhysicForce는 0.2초 뒤에 처음 잰 콜라이더를 다시 만지는데, 그때쯤 이 블랙홀과 죽은 좀비의 콜라이더가 이미 사라져 오류가 난다.
        // (제3자 에셋 스크립트를 고치지 않고, 이 인스턴스에서만 끈다.) 피해는 ApplyAreaDamage가 준다.
        foreach (PPFXPhysicForce physicForce in effect.GetComponentsInChildren<PPFXPhysicForce>(true))
        {
            physicForce.enabled = false;
            Destroy(physicForce);
        }

        ApplyAreaDamage();

        Exploded?.Invoke(this);
        Destroy(gameObject);
    }

    // 주변의 좀비와 다른 블랙홀(연쇄 폭발)에게 피해를 준다.
    private void ApplyAreaDamage()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _explosionRange, LayerMask.GetMask("Drum", "Zombie"));
        foreach (Collider col in colliders)
        {
            col.GetComponentInParent<BlackHole>()?.Damage(_damage);
            col.GetComponentInParent<Zombie>()?.TakeDamage(_damage);
        }
    }
}
