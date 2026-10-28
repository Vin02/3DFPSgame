using UnityEngine;

// 시작점에서 도착점까지 날아가는 짧은 빛줄기. 꼬리가 도착점에 닿으면 스스로 사라진다.
[RequireComponent(typeof(LineRenderer))]
public class BulletTracer : MonoBehaviour
{
    [SerializeField] private LineRenderer _line;
    [SerializeField] private float _speed = 150f;
    [SerializeField] private float _length = 3f;

    private Vector3 _start;
    private Vector3 _direction;
    private float _distance;
    private float _head;

    private void Awake()
    {
        if (_line == null)
        {
            _line = GetComponent<LineRenderer>();
        }

        _line.useWorldSpace = true;
        _line.positionCount = 2;
    }

    public void Launch(Vector3 start, Vector3 end)
    {
        _start = start;
        _distance = Vector3.Distance(start, end);
        _direction = _distance > 0.0001f ? (end - start) / _distance : Vector3.forward;
        _head = 0f;
        Draw();
    }

    private void Update()
    {
        _head += _speed * Time.deltaTime;

        if (_head - _length >= _distance)
        {
            Finish();
            return;
        }

        Draw();
    }

    // 풀에서 꺼낸 것이면 풀로 돌려보내고, 아니면 제거한다.
    private void Finish()
    {
        if (TryGetComponent(out PooledObject pooled))
        {
            pooled.Release();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Draw()
    {
        float tail = Mathf.Max(0f, _head - _length);
        _line.SetPosition(0, _start + _direction * tail);
        _line.SetPosition(1, _start + _direction * Mathf.Min(_head, _distance));
    }
}
