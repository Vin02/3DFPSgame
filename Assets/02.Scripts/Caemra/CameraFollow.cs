using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;

    private void Awake()
    {
        if (_target)
        {
            Debug.LogWarning("카메라없음");
        }
    }

    private void LateUpdate()
    {
        transform.position = _target.position;
    }
}
