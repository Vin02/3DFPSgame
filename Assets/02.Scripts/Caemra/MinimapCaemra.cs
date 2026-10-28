using System;
using UnityEngine;

public class MinimapCaemra : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _height = 8f;

    private void LateUpdate()
    {
        transform.position = _target.position + (Vector3.up * _height);
        transform.eulerAngles = new Vector3(90, _target.eulerAngles.y, 0);
    }
}
