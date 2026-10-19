using System;
using UnityEngine;
// 마우스를 움직이면 카메라를 그 방향으로 회전
public class CameraRotate : MonoBehaviour
{
    [SerializeField] private float _rotationSpeed = 200f;

    private float _accumulationX = 0f;
    private float _accumulationY = 0f;
    private void Update()
    {
        // 마우스 입력을 받고
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        // Debug.Log($"mouseX: {mouseX}, mouseY: {mouseY}");
        // 마우스 입력만큼 회전값을 누적한다.

        //Vector3 dir = new Vector3(-mouseY, mouseX, 0);    
        _accumulationX += mouseX * _rotationSpeed * Time.deltaTime;
        _accumulationY += -mouseY * _rotationSpeed * Time.deltaTime;

        // 누적 전에 내 변수에서 먼저 제한( -90 ~ 90 )
        _accumulationY = Mathf.Clamp(_accumulationY, -90, 90);

        //누적한 회전값을 카메라에 적용
        transform.eulerAngles = new Vector3(_accumulationY, _accumulationX, 0);
        // 그 방향으로 회전
        // 새로운 위치 = 현재 위치 + 속도 x 시간
        // 새로운 각도 = 현재 각도 + 각속도 x 시간
        //transform.eulerAngles += _rotationSpeed * dir * Time.deltaTime;

        // 4. 위 아래 회전값 제한 ( -90 ~ 90 )
        //Vector3 rotation = transform.eulerAngles;
        //정면 : 0도
        // 하늘을 살짝 1도만큼 쳐다보면 -1 -> 359 -> 90
        // 유니티는 0~360도 체계를 쓰기 때문에 음수는 자동으로 변환되어 반환.
        //rotation.x = Mathf.Clamp(rotation.x, -90, 90);
        //transform.eulerAngles = rotation;
    }
}
