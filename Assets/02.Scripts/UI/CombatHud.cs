using UnityEngine;
using UnityEngine.UI;

// 화면 위쪽에 지금 살아 있는 좀비 수와 처치 수를 표시한다. (좀비는 끝없이 나온다)
// 값이 바뀔 때만 글자를 다시 만든다. 총알·폭탄 표시는 WeaponHud가 맡는다.
public class CombatHud : MonoBehaviour
{
    [SerializeField] private ZombieManager _zombieManager;
    [SerializeField] private Text _zombieText;

    private int _shownAlive = -1;
    private int _shownKills = -1;

    private void Awake()
    {
        if (_zombieManager == null || _zombieText == null)
        {
            GameLogger.LogError("CombatHud에 필요한 참조가 없습니다.");
            enabled = false;
        }
    }

    private void Update()
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
}
