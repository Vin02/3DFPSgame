using UnityEngine;
using UnityEngine.UI;

// 화면 위쪽에 지금 살아 있는 좀비 수와 처치 수, 오른쪽 아래에 탄창의 남은 총알 수를 표시한다. (좀비는 끝없이 나온다)
// 값이 바뀔 때만 글자를 다시 만든다.
public class CombatHud : MonoBehaviour
{
    [SerializeField] private ZombieManager _zombieManager;
    [SerializeField] private PlayerGunFire _gun;

    [SerializeField] private Text _zombieText;
    [SerializeField] private Text _ammoText;
    [SerializeField] private Text _reloadText;

    private int _shownAlive = -1;
    private int _shownKills = -1;
    private int _shownAmmo = -1;
    private bool _shownReloading;

    private void Awake()
    {
        if (_zombieManager == null || _gun == null || _zombieText == null || _ammoText == null)
        {
            GameLogger.LogError("CombatHud에 필요한 참조가 없습니다.");
            enabled = false;
            return;
        }

        if (_reloadText != null)
        {
            _reloadText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        UpdateZombies();
        UpdateAmmo();
    }

    private void UpdateZombies()
    {
        int alive = _zombieManager.AliveCount;
        int kills = _zombieManager.KillCount;
        if (alive == _shownAlive && kills == _shownKills)
        {
            return;
        }

        _shownAlive = alive;
        _shownKills = kills;
        _zombieText.text = $"ZOMBIES  {alive}     KILLS  {kills}";
    }

    private void UpdateAmmo()
    {
        int ammo = _gun.Ammo;
        if (ammo != _shownAmmo)
        {
            _shownAmmo = ammo;
            _ammoText.text = $"{ammo} / {_gun.MagazineSize}";
            _ammoText.color = ammo <= _gun.MagazineSize / 5 ? new Color(0.9f, 0.2f, 0.15f) : Color.white;
        }

        bool reloading = _gun.IsReloading;
        if (_reloadText != null && reloading != _shownReloading)
        {
            _shownReloading = reloading;
            _reloadText.gameObject.SetActive(reloading);
        }
    }
}
