using UnityEngine;
using UnityEngine.UI;

// 체력 바와 스태미너 바를 화면에 표시한다. 바는 채움 사각형의 오른쪽 앵커를 비율만큼 움직여 줄인다.
public class PlayerStatusHud : MonoBehaviour
{
    [SerializeField] private PlayerHealth _health;
    [SerializeField] private PlayerMove _move;

    [Header("체력")]
    [SerializeField] private RectTransform _healthFill;
    [SerializeField] private Text _healthText;

    [Header("스태미너")]
    [SerializeField] private RectTransform _staminaFill;
    [SerializeField] private Text _staminaText;

    private float _shownHealthRatio = -1f;
    private float _shownStaminaRatio = -1f;
    private int _shownHealthValue = -1;
    private int _shownStaminaValue = -1;

    private void Awake()
    {
        if (_health == null || _move == null || _healthFill == null || _staminaFill == null)
        {
            GameLogger.LogError("PlayerStatusHud에 필요한 참조가 없습니다.");
            enabled = false;
        }
    }

    private void Update()
    {
        UpdateHealth();
        UpdateStamina();
    }

    private void UpdateHealth()
    {
        float ratio = _health.Ratio;
        if (!Mathf.Approximately(ratio, _shownHealthRatio))
        {
            _shownHealthRatio = ratio;
            SetFill(_healthFill, ratio);
        }

        int value = Mathf.CeilToInt(_health.Current);
        if (_healthText != null && value != _shownHealthValue)
        {
            _shownHealthValue = value;
            _healthText.text = $"HP {value} / {Mathf.CeilToInt(_health.Max)}";
        }
    }

    private void UpdateStamina()
    {
        float ratio = _move.GetStaminaRatio();
        if (!Mathf.Approximately(ratio, _shownStaminaRatio))
        {
            _shownStaminaRatio = ratio;
            SetFill(_staminaFill, ratio);
        }

        int percent = Mathf.RoundToInt(ratio * 100f);
        if (_staminaText != null && percent != _shownStaminaValue)
        {
            _shownStaminaValue = percent;
            _staminaText.text = $"STAMINA {percent}%";
        }
    }

    private static void SetFill(RectTransform fill, float ratio)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
    }
}
