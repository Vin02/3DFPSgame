using UnityEngine;

// 미니맵 아이콘이 소유자의 회전과 상관없이 미니맵 카메라 기준 위쪽을 향하도록 유지한다.
// 씬에 놓인 아이콘은 직렬화된 참조를 쓰고, 나중에 생성된 아이콘은 Initialize로 카메라를 받는다.
public class MinimapIcon : MonoBehaviour
{
    [SerializeField] private Transform _minimapCamera;

    public void Initialize(Transform minimapCamera)
    {
        _minimapCamera = minimapCamera;
    }

    private void LateUpdate()
    {
        if (_minimapCamera == null)
        {
            return;
        }

        transform.rotation = Quaternion.Euler(90f, _minimapCamera.eulerAngles.y, 0f);
    }
}
