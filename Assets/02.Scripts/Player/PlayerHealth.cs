using UnityEngine;

// 플레이어의 체력. 체력이 일정 비율 이하로 내려가면 부상 상태가 된다.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    [Tooltip("체력 비율이 이 값 이하이면 부상 상태 (절뚝이는 애니메이션)")]
    [SerializeField, Range(0f, 1f)] private float _injuredRatio = 0.5f;

    private float _currentHealth;

    public float Current => _currentHealth;
    public float Max => _maxHealth;
    public float Ratio => _maxHealth > 0f ? _currentHealth / _maxHealth : 0f;
    public bool IsInjured => Ratio <= _injuredRatio;
    public bool IsDead => _currentHealth <= 0f;

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || damage <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        GameLogger.Log($"플레이어 체력 {_currentHealth}/{_maxHealth}");

        if (IsDead)
        {
            GameLogger.Log("플레이어가 쓰러졌습니다.");
        }
    }
}
