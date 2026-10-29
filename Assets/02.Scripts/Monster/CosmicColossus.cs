using System;
using UnityEngine;

// 맵 바깥 아득히 먼 곳에 서 있는, 지평선을 덮을 만큼 거대한 눈 덩어리.
//  - 움직이지 않고, 체력이 무한하다. (총알은 맞지만 아무 일도 일어나지 않는다)
//  - 몸에 돋은 수많은 눈이 모두 플레이어를 쳐다보고, 제각각 깜빡인다.
//  - 몸 전체가 아주 느리게 숨 쉬듯 부풀었다 가라앉는다.
// 눈·살덩이 오브젝트는 CosmicColossusBuilder가 만들어 연결한다.
public class CosmicColossus : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _body;
    [SerializeField] private Transform[] _eyes;

    [Header("눈")]
    [SerializeField] private float _eyeLookLimit = 80f;
    [SerializeField] private float _eyeLookSharpness = 1.5f;
    [Tooltip("눈 하나가 깜빡이기까지의 간격(초)")]
    [SerializeField] private Vector2 _blinkInterval = new Vector2(3f, 12f);
    [SerializeField] private float _blinkDuration = 0.5f;

    [Header("숨쉬기")]
    [SerializeField] private float _breathAmount = 0.012f;
    [SerializeField] private float _breathSpeed = 0.25f;

    // 총알이 맞았을 때 알린다. (체력은 줄지 않는다)
    public event Action<Vector3> Hit;

    public int HitCount { get; private set; }

    private Quaternion[] _eyeRestRotation;
    private Vector3[] _eyeScale;
    private float[] _nextBlinkTime;
    private Vector3 _bodyScale;

    private void Awake()
    {
        if (_target == null || _body == null || _eyes == null || _eyes.Length == 0)
        {
            GameLogger.LogError("CosmicColossus에 필요한 참조가 없습니다.");
            enabled = false;
            return;
        }

        int count = _eyes.Length;
        _eyeRestRotation = new Quaternion[count];
        _eyeScale = new Vector3[count];
        _nextBlinkTime = new float[count];
        for (int i = 0; i < count; i++)
        {
            _eyeRestRotation[i] = _eyes[i].rotation;
            _eyeScale[i] = _eyes[i].localScale;
            _nextBlinkTime[i] = UnityEngine.Random.Range(0f, _blinkInterval.y);
        }

        _bodyScale = _body.localScale;
    }

    // 체력이 무한하므로 피해량은 쓰지 않는다. 맞았다는 사실만 알린다.
    public void TakeDamage(float damage, Vector3 point)
    {
        HitCount++;
        Hit?.Invoke(point);
    }

    private void LateUpdate()
    {
        // 몸 전체가 아주 느리게 숨 쉰다. (눈은 몸의 자식이라 함께 움직인다)
        float breath = 1f + Mathf.Sin(Time.time * _breathSpeed) * _breathAmount;
        _body.localScale = _bodyScale * breath;

        Vector3 targetPosition = _target.position;
        float blend = 1f - Mathf.Exp(-_eyeLookSharpness * Time.deltaTime);
        for (int i = 0; i < _eyes.Length; i++)
        {
            Transform eye = _eyes[i];
            Vector3 toTarget = targetPosition - eye.position;
            Quaternion look = Quaternion.LookRotation(toTarget);
            Quaternion limited = Quaternion.RotateTowards(_eyeRestRotation[i], look, _eyeLookLimit);
            eye.rotation = Quaternion.Slerp(eye.rotation, limited, blend);

            // 깜빡임: 세로로 납작해졌다가 돌아온다.
            float sinceBlink = Time.time - _nextBlinkTime[i];
            float open = 1f;
            if (sinceBlink >= 0f)
            {
                if (sinceBlink > _blinkDuration)
                {
                    _nextBlinkTime[i] = Time.time + UnityEngine.Random.Range(_blinkInterval.x, _blinkInterval.y);
                }
                else
                {
                    open = Mathf.Abs(sinceBlink / _blinkDuration * 2f - 1f);
                }
            }

            Vector3 scale = _eyeScale[i];
            eye.localScale = new Vector3(scale.x, scale.y * Mathf.Max(open, 0.06f), scale.z);
        }
    }
}
