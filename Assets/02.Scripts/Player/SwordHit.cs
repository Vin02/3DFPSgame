using UnityEngine;

// 손에 든 칼. 플레이어와 함께 움직이면서 적에게 닿으면 PlayeMeleeAttack에 알린다.
// 칼은 물리로 움직이지 않으므로 Rigidbody는 중력을 끄고 Is Kinematic을 켠다. (트리거 충돌 이벤트를 받으려면 Rigidbody가 필요하다)
// 콜라이더는 칼날(blade) 쪽에만 BoxCollider(트리거)를 만든다. 이미 붙어 있으면 그대로 쓴다.
// 좀비는 CharacterController라서 일반 충돌은 안 일어나므로 트리거 겹침으로 닿음을 판정한다.
public class SwordHit : MonoBehaviour
{
    [Header("Blade (칼 메시 로컬 기준, 칼끝은 -Z 방향)")]
    [Tooltip("칼 전체 길이 중 칼끝에서부터 날이 차지하는 비율. 나머지는 손잡이·날밑이라 제외한다.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _bladeLengthRatio = 0.6f;
    [Tooltip("칼 메시의 가로·세로 크기 중 칼날 콜라이더가 차지하는 비율 (X, Y).")]
    [SerializeField] private Vector2 _bladeThicknessRatio = new Vector2(0.4f, 0.4f);

    private PlayeMeleeAttack _attack;
    private Collider _collider;

    private void Awake()
    {
        // 칼날이 조준·총알 레이캐스트에 걸리지 않도록 Ignore Raycast 레이어로 둔다. (충돌 판정은 레이어와 무관하게 동작한다)
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        if (!TryGetComponent(out Rigidbody body))
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.isKinematic = true;

        if (!TryGetComponent(out Collider swordCollider))
        {
            var box = gameObject.AddComponent<BoxCollider>();
            if (TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            {
                Bounds bounds = meshFilter.sharedMesh.bounds;
                float bladeLength = bounds.size.z * _bladeLengthRatio;
                box.center = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z + bladeLength * 0.5f);
                box.size = new Vector3(bounds.size.x * _bladeThicknessRatio.x, bounds.size.y * _bladeThicknessRatio.y, bladeLength);
            }

            swordCollider = box;
        }

        swordCollider.isTrigger = true;
        // 평소에는 꺼 두고, 휘두르는 구간에만 PlayeMeleeAttack이 켠다.
        swordCollider.enabled = false;
        _collider = swordCollider;
    }

    public void SetColliderEnabled(bool value)
    {
        if (_collider != null)
        {
            _collider.enabled = value;
        }
    }

    // PlayeMeleeAttack이 실행 중에 자동으로 붙을 수 있어서 Awake가 아니라 Start에서 찾는다.
    private void Start()
    {
        _attack = GetComponentInParent<PlayeMeleeAttack>();
        if (_attack == null)
        {
            GameLogger.LogError("SwordHit: 부모에서 PlayeMeleeAttack을 찾을 수 없습니다.");
            enabled = false;
        }
    }

    // 베기 구간이 시작되기 전부터 칼 안에 있던 적도 맞도록 Stay에서도 넘긴다.
    private void OnTriggerStay(Collider other)
    {
        if (_attack == null)
        {
            return;
        }

        _attack.Hit(other, other.ClosestPoint(transform.position));
    }
}
