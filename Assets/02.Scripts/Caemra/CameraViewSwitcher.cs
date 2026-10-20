using UnityEngine;

// 입력 키를 누를 때마다 카메라 시점을 순서대로 전환한다.
public class CameraViewSwitcher : MonoBehaviour
{
    [SerializeField] private CameraFollow _cameraFollow;
    [SerializeField] private KeyCode _switchKey = KeyCode.V;
    [SerializeField] private CameraView[] _views = CreateDefaultViews();

    private int _currentIndex;

    private void Awake()
    {
        if (_cameraFollow == null)
        {
            _cameraFollow = GetComponent<CameraFollow>();
        }

        if (_cameraFollow == null || _views.Length == 0)
        {
            GameLogger.LogWarning("CameraFollow 또는 시점 목록이 없어 시점 전환을 사용할 수 없습니다.");
            enabled = false;
        }
    }

    private void Start()
    {
        ApplyCurrentView();
    }

    private void Update()
    {
        if (Input.GetKeyDown(_switchKey))
        {
            SwitchToNextView();
        }
    }

    private void SwitchToNextView()
    {
        _currentIndex = (_currentIndex + 1) % _views.Length;
        ApplyCurrentView();
    }

    private void ApplyCurrentView()
    {
        CameraView view = _views[_currentIndex];
        GameLogger.Log($"카메라 시점 전환: {view.Name}");
        _cameraFollow.SetView(view);
    }

    // 1인칭 -> 3인칭 -> 숄더뷰 -> 사이드뷰 -> 탑다운뷰 -> 쿼터뷰 -> 1인칭
    private static CameraView[] CreateDefaultViews()
    {
        return new[]
        {
            CameraView.CreateFreeLook("1인칭", Vector3.zero),
            CameraView.CreateFreeLook("3인칭", new Vector3(0f, 1f, -4f)),
            CameraView.CreateFreeLook("숄더뷰", new Vector3(0.7f, 0.5f, -1.8f)),
            CameraView.CreateFixed("사이드뷰", new Vector3(0f, 1f, -8f), new Vector3(0f, 90f, 0f)),
            CameraView.CreateFixed("탑다운뷰", new Vector3(0f, 0f, -12f), new Vector3(90f, 0f, 0f)),
            CameraView.CreateFixed("쿼터뷰", new Vector3(0f, 0f, -12f), new Vector3(35f, 45f, 0f)),
        };
    }
}
