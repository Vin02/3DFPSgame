using System;
using UnityEngine;

// 카메라 시점 하나의 설정.
// 카메라 위치 = 대상 위치 + 카메라 회전 * Offset
[Serializable]
public class CameraView
{
    [SerializeField] private string _name;
    [SerializeField] private Vector3 _offset;
    [SerializeField] private bool _isFreeLook;
    [SerializeField] private Vector3 _fixedEulerAngles;

    private CameraView(string name, Vector3 offset, bool isFreeLook, Vector3 fixedEulerAngles)
    {
        _name = name;
        _offset = offset;
        _isFreeLook = isFreeLook;
        _fixedEulerAngles = fixedEulerAngles;
    }

    public string Name => _name;
    public Vector3 Offset => _offset;
    public bool IsFreeLook => _isFreeLook;
    public Quaternion FixedRotation => Quaternion.Euler(_fixedEulerAngles);

    // 마우스로 회전하는 시점 (1인칭, 3인칭, 숄더뷰)
    public static CameraView CreateFreeLook(string name, Vector3 offset)
    {
        return new CameraView(name, offset, true, Vector3.zero);
    }

    // 회전이 고정된 시점 (사이드뷰, 탑다운뷰, 쿼터뷰)
    public static CameraView CreateFixed(string name, Vector3 offset, Vector3 eulerAngles)
    {
        return new CameraView(name, offset, false, eulerAngles);
    }
}
