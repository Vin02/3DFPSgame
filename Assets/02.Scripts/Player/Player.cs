using System;
using UnityEngine;

// 플레이어의 현재 무기 모드를 관리한다. 1번 키는 일반, 2번 키는 저격, 3번 키는 근접(Melee) 모드.
// 모드가 바뀌면 이벤트로 알린다. (PlayerZoom이 이 값을 그대로 노출하므로 기존 구독자는 PlayerZoom을 통해서도 받을 수 있다)
public class Player : MonoBehaviour
{
    [SerializeField] private KeyCode _normalKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode _sniperKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode _meleeKey = KeyCode.Alpha3;

    private WeaponMode _mode = WeaponMode.Normal;

    public event Action<WeaponMode> ModeChanged;

    public WeaponMode Mode => _mode;

    private void Awake()
    {
        // 씬에 근접 공격이 아직 붙어 있지 않으면 같은 오브젝트에 자동으로 붙인다.
        if (!TryGetComponent<PlayeMeleeAttack>(out _))
        {
            gameObject.AddComponent<PlayeMeleeAttack>();
        }
    }

    private void Update()
    {
        if (GameManager.IsPaused)
        {
            return;
        }

        if (Input.GetKeyDown(_normalKey))
        {
            SetMode(WeaponMode.Normal);
        }
        else if (Input.GetKeyDown(_sniperKey))
        {
            SetMode(WeaponMode.Sniper);
        }
        else if (Input.GetKeyDown(_meleeKey))
        {
            SetMode(WeaponMode.Melee);
        }
    }

    public void SetMode(WeaponMode mode)
    {
        if (_mode == mode)
        {
            return;
        }

        _mode = mode;
        ModeChanged?.Invoke(_mode);
    }
}
