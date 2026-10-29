using UnityEngine;
using UnityEngine.UI;

// 오른쪽 아래에 현재 무기 이미지, 현재/최대 총알 수, 남은 폭탄 수를 보여 준다.
// 옵저버 패턴: 총·섬광탄·무기 모드가 보내는 이벤트를 구독해서, 값이 바뀔 때만 UI를 고친다. (매 프레임 확인하지 않는다)
public class WeaponHud : MonoBehaviour
{
    [Header("Subject (이벤트를 보내는 쪽)")]
    [SerializeField] private PlayerZoom _zoom;
    [SerializeField] private PlayerGunFire _gun;
    [SerializeField] private PlayerFlashbangThrow _flashbang;

    [Header("UI")]
    [SerializeField] private Image _weaponIcon;
    [SerializeField] private Text _ammoText;
    [SerializeField] private Text _bombText;
    [SerializeField] private Text _reloadText;
    [Tooltip("다음에 던질 수류탄 이름, 다 던졌으면 장전 안내")]
    [SerializeField] private Text _grenadeText;

    [Header("Weapon Sprites")]
    [SerializeField] private Sprite _normalWeaponSprite;
    [SerializeField] private Sprite _sniperWeaponSprite;

    private bool _started;

    private static readonly Color LowAmmoColor = new Color(0.9f, 0.2f, 0.15f);

    private void Awake()
    {
        if (_zoom == null || _gun == null || _flashbang == null || _weaponIcon == null || _ammoText == null || _bombText == null
            || _normalWeaponSprite == null || _sniperWeaponSprite == null)
        {
            GameLogger.LogError("WeaponHud에 필요한 참조가 없습니다.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        _zoom.ModeChanged += OnModeChanged;
        _gun.AmmoChanged += OnAmmoChanged;
        _gun.ReloadingChanged += OnReloadingChanged;
        _flashbang.BombsChanged += OnBombsChanged;
        _flashbang.ReloadingChanged += OnGrenadeReloadingChanged;

        // 다시 켜졌을 때는 꺼져 있는 동안 놓친 값을 바로 그린다.
        if (_started)
        {
            Refresh();
        }
    }

    // 총·섬광탄의 초기 값은 그쪽 Awake에서 정해지므로, 첫 그리기는 모든 Awake가 끝난 Start에서 한다.
    private void Start()
    {
        _started = true;
        Refresh();
    }

    private void OnDisable()
    {
        _zoom.ModeChanged -= OnModeChanged;
        _gun.AmmoChanged -= OnAmmoChanged;
        _gun.ReloadingChanged -= OnReloadingChanged;
        _flashbang.BombsChanged -= OnBombsChanged;
        _flashbang.ReloadingChanged -= OnGrenadeReloadingChanged;
    }

    private void Refresh()
    {
        OnModeChanged(_zoom.Mode);
        OnAmmoChanged(_gun.Ammo, _gun.MagazineSize);
        OnReloadingChanged(_gun.IsReloading);
        OnBombsChanged(_flashbang.Bombs, _flashbang.MaxBombs);
    }

    private void OnModeChanged(WeaponMode mode)
    {
        _weaponIcon.sprite = mode == WeaponMode.Sniper ? _sniperWeaponSprite : _normalWeaponSprite;
    }

    private void OnAmmoChanged(int ammo, int magazineSize)
    {
        _ammoText.text = $"{ammo} / {magazineSize}";
        _ammoText.color = ammo <= magazineSize / 5 ? LowAmmoColor : Color.white;
    }

    private void OnReloadingChanged(bool reloading)
    {
        if (_reloadText != null)
        {
            _reloadText.gameObject.SetActive(reloading);
        }
    }

    private void OnBombsChanged(int bombs, int maxBombs)
    {
        _bombText.text = $"x {bombs}";
        _bombText.color = bombs <= 0 ? LowAmmoColor : Color.white;
        UpdateGrenadeText();
    }

    private void OnGrenadeReloadingChanged(bool reloading)
    {
        UpdateGrenadeText();
    }

    private void UpdateGrenadeText()
    {
        if (_grenadeText == null)
        {
            return;
        }

        if (_flashbang.IsReloading)
        {
            _grenadeText.text = "RELOADING...";
            _grenadeText.color = new Color(1f, 0.8f, 0.3f);
        }
        else if (_flashbang.Bombs <= 0)
        {
            _grenadeText.text = "[G] RELOAD";
            _grenadeText.color = LowAmmoColor;
        }
        else
        {
            _grenadeText.text = _flashbang.NextName;
            _grenadeText.color = Color.white;
        }
    }
}
