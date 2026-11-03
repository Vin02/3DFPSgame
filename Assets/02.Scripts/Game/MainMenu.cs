using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// 메인화면. 연출 영상을 재생하고, 영상 속 로그인 화면이 나타나는 시점(_loginScreenTime)부터 버튼을 활성화한다.
// LOG IN → 시작 씬, QUIT → 게임 종료. 버튼은 영상에 그려진 버튼 위에 겹쳐 둔 투명 버튼이고, 호버 효과는 HoverHighlight가 낸다.
// 영상은 처음부터 끝까지 그대로 재생한다(건너뛰기 없음). 로그인 기능은 없다.
public class MainMenu : MonoBehaviour
{
    [SerializeField] private VideoPlayer _video;
    [SerializeField] private CanvasGroup _buttonGroup;
    [SerializeField] private Button _loginButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private string _startSceneName = "StartScene";
    [Tooltip("영상에서 로그인 화면(LOG IN/QUIT 버튼)이 보이기 시작하는 시간(초). 이때부터 버튼을 누를 수 있다.")]
    [SerializeField] private float _loginScreenTime = 15.5f;

    private bool _buttonsShown;

    private void Awake()
    {
        if (_video == null || _buttonGroup == null || _loginButton == null || _quitButton == null)
        {
            GameLogger.LogError("MainMenu에 필요한 참조가 없습니다.");
            enabled = false;
            return;
        }

        GameManager.MenuMode = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetButtonsActive(false);
    }

    private void OnEnable()
    {
        _video.loopPointReached += OnVideoEnded;
        _video.errorReceived += OnVideoError;
        _loginButton.onClick.AddListener(OnLoginClicked);
        _quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnDisable()
    {
        _video.loopPointReached -= OnVideoEnded;
        _video.errorReceived -= OnVideoError;
        _loginButton.onClick.RemoveListener(OnLoginClicked);
        _quitButton.onClick.RemoveListener(OnQuitClicked);
    }

    private void OnDestroy()
    {
        GameManager.MenuMode = false;
    }

    private void Update()
    {
        if (_buttonsShown || !_video.isPrepared)
        {
            return;
        }

        if (_video.isPlaying && _video.time >= _loginScreenTime)
        {
            SetButtonsActive(true);
        }
    }

    private void OnVideoEnded(VideoPlayer source)
    {
        SetButtonsActive(true);
    }

    // 영상이 재생되지 않아도 메뉴는 쓸 수 있게 한다.
    private void OnVideoError(VideoPlayer source, string message)
    {
        GameLogger.LogError($"메인화면 영상 재생 실패: {message}");
        SetButtonsActive(true);
    }

    private void SetButtonsActive(bool active)
    {
        _buttonsShown = active;
        _buttonGroup.interactable = active;
        _buttonGroup.blocksRaycasts = active;
    }

    private void OnLoginClicked()
    {
        GameManager.Instance.LoadScene(_startSceneName);
    }

    private void OnQuitClicked()
    {
        GameManager.Instance.Quit();
    }
}
