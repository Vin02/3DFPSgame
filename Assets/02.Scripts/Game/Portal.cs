using UnityEngine;

// 플레이어가 가까이 오면 "E 키를 눌러 입장" 안내를 띄우고, E를 누르면 지정한 씬으로 이동한다.
// 이동할 씬은 Build Settings에 등록되어 있어야 한다.
public class Portal : MonoBehaviour
{
    [SerializeField] private Transform _player;
    [SerializeField] private string _targetSceneName = "GameScene";
    [SerializeField] private float _interactDistance = 3f;
    [SerializeField] private KeyCode _interactKey = KeyCode.E;
    [SerializeField] private string _promptText = "E 키를 눌러 입장";

    private bool _inRange;
    private GUIStyle _promptStyle;

    private void Awake()
    {
        if (_player == null || string.IsNullOrEmpty(_targetSceneName))
        {
            GameLogger.LogError("Portal에 플레이어 또는 이동할 씬 이름이 없습니다.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (GameManager.IsPaused)
        {
            return;
        }

        _inRange = (_player.position - transform.position).sqrMagnitude <= _interactDistance * _interactDistance;

        if (_inRange && Input.GetKeyDown(_interactKey))
        {
            GameManager.Instance.LoadScene(_targetSceneName);
        }
    }

    private void OnGUI()
    {
        if (!_inRange || GameManager.IsPaused)
        {
            return;
        }

        _promptStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(0f, Screen.height * 0.62f, Screen.width, 60f), _promptText, _promptStyle);
    }
}
