using UnityEngine;

// 마우스 입력을 누적해 자유 시점 회전값을 계산한다.
// 카메라에 적용하는 일은 CameraFollow가 맡는다.
// 고정 시점 중에도 계속 누적해서 PlayerRotate와 좌우 방향이 어긋나지 않게 한다.
public class CameraRotate : MonoBehaviour
{
    private const float MaxPitch = 90f;

    [SerializeField] private float _rotationSpeed = 200f;

    private float _yaw;
    private float _pitch;

    public Quaternion Rotation => Quaternion.Euler(_pitch, _yaw, 0f);

    private void Update()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        _yaw += mouseX * _rotationSpeed * Time.deltaTime;
        _pitch -= mouseY * _rotationSpeed * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, -MaxPitch, MaxPitch);
    }
}
