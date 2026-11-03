using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 전체의 진행 상태(플레이/일시정지), 커서 잠금, 시간 배율, 재시작, 종료를 한 곳에서 관리한다.
// 씬 수정 없이 게임 시작 시 자동 생성되며 씬이 바뀌어도 유지된다.
// ESC: 일시정지 토글. 화면은 MenuPanel(일시정지·게임오버)이 맡는다.
public class GameManager : SingletonBehaviour<GameManager>
{
    // 어디서든 입력 가드로 쓴다: if (GameManager.IsPaused) return;
    // 게임오버 중에도 시간이 멈춰 있으므로 true다.
    public static bool IsPaused => Instance != null && (Instance._paused || Instance._gameOver);

    public static bool IsGameOver => Instance != null && Instance._gameOver;

    // 메인화면처럼 마우스로 조작하는 씬이 켜 둔다. true면 ESC 일시정지를 막고 커서를 항상 풀어 둔다.
    // (Instance보다 먼저 설정될 수 있어 static으로 둔다. 설정한 쪽이 씬을 떠날 때 false로 되돌린다.)
    public static bool MenuMode { get; set; }

    private bool _paused;
    private bool _gameOver;
    private MenuPanel _pauseMenu;
    private MenuPanel _gameOverMenu;

    public event Action<bool> PausedChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn() => CreatePersistent("[GameManager]");

    protected override void OnSingletonAwake()
    {
        _pauseMenu = new MenuPanel(transform, "PauseMenuCanvas", "일시정지",
            new Color(0f, 0f, 0f, 0.45f), Color.white, 1000,
            new MenuPanel.Item("계속하기", Resume),
            new MenuPanel.Item("다시 시작", Restart),
            new MenuPanel.Item("게임 종료", Quit));
        _gameOverMenu = new MenuPanel(transform, "GameOverCanvas", "게임 오버",
            new Color(0.25f, 0f, 0f, 0.6f), new Color(1f, 0.3f, 0.3f), 1001,
            new MenuPanel.Item("다시 시작", Restart),
            new MenuPanel.Item("게임 종료", Quit));
        ApplyState(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    protected override void OnSingletonDestroy()
    {
        // 에디터에서 플레이를 끝낼 때 시간 배율이 0으로 남지 않게 한다.
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !_gameOver && !MenuMode)
        {
            SetPaused(!_paused);
        }
    }

    // 씬이 새로 로드되면(재시작) 항상 플레이 상태로 시작한다.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _gameOver = false;
        SetPaused(false);
    }

    // 플레이어 HP가 0이 되면 PlayerHealth가 호출한다. 게임을 멈추고 게임오버 창을 띄운다.
    public void GameOver()
    {
        if (_gameOver)
        {
            return;
        }

        _gameOver = true;
        // 일시정지 창이 떠 있었다면 닫는다.
        _paused = false;
        _pauseMenu.SetVisible(false);
        Freeze(true);
        _gameOverMenu.SetVisible(true);
    }

    public void SetPaused(bool paused)
    {
        if (_gameOver)
        {
            return;
        }

        if (_paused == paused)
        {
            // 상태는 같아도 커서/시간은 항상 올바르게 맞춘다. (씬 로드 직후 등)
            ApplyState(paused);
            return;
        }

        _paused = paused;
        ApplyState(paused);
        PausedChanged?.Invoke(paused);
    }

    private void ApplyState(bool paused)
    {
        Freeze(paused);
        _pauseMenu?.SetVisible(paused);
        _gameOverMenu?.SetVisible(false);
    }

    // 시간·소리를 멈추고 커서를 풀거나(true), 반대로 되돌린다(false).
    private static void Freeze(bool frozen)
    {
        Time.timeScale = frozen ? 0f : 1f;
        AudioListener.pause = frozen;
        SetCursorLocked(!frozen && !MenuMode);
    }

    public void LockCursor() => SetCursorLocked(true);

    public void UnlockCursor() => SetCursorLocked(false);

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void Resume()
    {
        SetPaused(false);
    }

    public void Restart()
    {
        _gameOver = false;
        SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // 지정한 씬으로 이동한다. (포탈 등) 상태 초기화는 OnSceneLoaded에서 처리된다.
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
