using UnityEngine;

// 몸에서 돋아나 꿈틀거리는 촉수. 매 프레임 굽은 관 모양 메시를 다시 만든다.
// 뿌리는 이 오브젝트의 위치이고, 로컬 앞(Z) 방향으로 뻗어 나가며 끝으로 갈수록 가늘어진다.
// 배열은 미리 만들어 재사용하므로 매 프레임 할당이 없다.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Tentacle : MonoBehaviour
{
    [SerializeField] private int _segments = 14;
    [SerializeField] private int _sides = 7;
    [SerializeField] private float _length = 0.7f;
    [SerializeField] private float _baseRadius = 0.03f;
    [SerializeField] private float _tipRadius = 0.003f;

    [Header("꿈틀거림")]
    [Tooltip("마디마다 휘는 최대 각도(도)")]
    [SerializeField] private float _bendAngle = 14f;
    [SerializeField] private float _waveSpeed = 1.7f;
    [SerializeField] private float _waveFrequency = 0.9f;

    private Mesh _mesh;
    private Vector3[] _vertices;
    private Vector3[] _normals;
    private float _phase;

    private void Awake()
    {
        _phase = Random.value * 100f;

        int ringCount = _segments + 1;
        _vertices = new Vector3[ringCount * _sides];
        _normals = new Vector3[_vertices.Length];

        var triangles = new int[_segments * _sides * 6];
        int t = 0;
        for (int segment = 0; segment < _segments; segment++)
        {
            for (int side = 0; side < _sides; side++)
            {
                int a = segment * _sides + side;
                int b = segment * _sides + (side + 1) % _sides;
                int c = a + _sides;
                int d = b + _sides;
                triangles[t++] = a;
                triangles[t++] = c;
                triangles[t++] = b;
                triangles[t++] = b;
                triangles[t++] = c;
                triangles[t++] = d;
            }
        }

        _mesh = new Mesh { name = "Tentacle" };
        _mesh.MarkDynamic();
        _mesh.vertices = _vertices;
        _mesh.triangles = triangles;
        // 매 프레임 경계를 다시 계산하지 않도록 넉넉하게 잡는다.
        _mesh.bounds = new Bounds(Vector3.forward * _length * 0.5f, Vector3.one * _length * 2.5f);
        GetComponent<MeshFilter>().sharedMesh = _mesh;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
        {
            Destroy(_mesh);
        }
    }

    private void LateUpdate()
    {
        float time = Time.time * _waveSpeed + _phase;
        float segmentLength = _length / _segments;

        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;

        for (int ring = 0; ring <= _segments; ring++)
        {
            float along = (float)ring / _segments;
            float radius = Mathf.Lerp(_baseRadius, _tipRadius, Mathf.Pow(along, 0.7f));

            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            int start = ring * _sides;
            for (int side = 0; side < _sides; side++)
            {
                float angle = side * Mathf.PI * 2f / _sides;
                Vector3 normal = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                _vertices[start + side] = position + normal * radius;
                _normals[start + side] = normal;
            }

            // 다음 마디: 두 방향의 물결을 겹쳐 끝으로 갈수록 크게 휜다.
            float wave = time - ring * _waveFrequency;
            float pitch = Mathf.Sin(wave) * _bendAngle * (0.4f + along);
            float yaw = Mathf.Sin(wave * 0.73f + 1.7f) * _bendAngle * (0.4f + along);
            rotation *= Quaternion.Euler(pitch, yaw, 0f);
            position += rotation * Vector3.forward * segmentLength;
        }

        _mesh.vertices = _vertices;
        _mesh.normals = _normals;
    }
}
