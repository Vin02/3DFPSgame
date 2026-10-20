using System;
using UnityEngine;

//마우스 왼쪽 버튼 누르면 바라보는 방향으로 총알 발사
public class PlayerGunFire : MonoBehaviour
{
    [SerializeField] private ParticleSystem _hitEffectPrefab;
    [Header("Stat")]
    [SerializeField] private float _range = 100f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _fireInterval = 0.1f;
    private float _fireTimer = 0f;

    private void Update()
    {
        _fireTimer -= Time.deltaTime;
        // 1. 마우스 왼쪽 버튼 입력 받기
        if (Input.GetButton("Fire1") && _fireTimer <= 0f)
        {
            _fireTimer = _fireInterval;
            Fire();
        }
    }

    private void Fire()
    {
        // 2. 레이(광선)를 생성하고 발사될 위치와 진행 뱡향 설정
        Transform cameraTransform = Camera.main.transform;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        // 3. 레이가 부딪히는 대상의 정보 저장할 변수 생성
        RaycastHit hitInfo = new RaycastHit();
        // 4.레이를 발사하고 반양 부딪힌 물체 있다면 이펙트 표시
        bool isHit = Physics.Raycast(ray, out hitInfo, _range);
        if (isHit)
        {
            //Debug.Log($"Hit {hitInfo.collider.gameObject.name}이 총알을 맞았습니다. 데미지 : {_damage}");
            ParticleSystem effect = Instantiate(_hitEffectPrefab);
            effect.transform.position = hitInfo.point;
            effect.transform.forward = hitInfo.normal;
            effect.Play();
        }
    }
}
