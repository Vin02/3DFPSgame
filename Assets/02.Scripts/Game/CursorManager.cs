using UnityEngine;

// 커서 잠금/해제는 이제 GameManager가 관리한다.
// 씬에 이미 붙어 있는 컴포넌트가 깨지지 않도록 남겨둔 호환용 래퍼이며, 새 코드는 GameManager를 직접 쓴다.
public class CursorManager : MonoBehaviour
{
    private void Start()
    {
        // GameManager는 씬 로드 직후 생성되므로 Awake가 아니라 Start에서 호출한다.
        if (GameManager.Instance != null && !GameManager.IsPaused)
        {
            GameManager.Instance.LockCursor();
        }
    }

    public void LockCursor()
    {
        GameManager.Instance?.LockCursor();
    }

    public void UnLockCursor()
    {
        GameManager.Instance?.UnlockCursor();
    }
}
