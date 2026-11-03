using UnityEngine;

// 씬에 하나만 존재하는 MonoBehaviour의 공통 뼈대. Instance 보관, 중복 제거, 해제를 맡는다.
// 사용법:
//   public class Foo : SingletonBehaviour<Foo>
//   {
//       [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
//       private static void Spawn() => CreatePersistent("[Foo]");   // 씬 수정 없이 자동 생성, 씬 전환 후에도 유지
//
//       protected override void OnSingletonAwake() { ... }          // Awake 대신 사용
//       protected override void OnSingletonDestroy() { ... }        // OnDestroy 대신 사용
//   }
// 파생 클래스에서 Awake/OnDestroy를 직접 정의하면 안 된다. (위 훅을 쓴다)
public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
{
    public static T Instance { get; private set; }

    public static bool HasInstance => Instance != null;

    // 이미 있으면 그대로 돌려주고, 없으면 새 오브젝트에 만들어 씬이 바뀌어도 유지한다.
    protected static T CreatePersistent(string objectName)
    {
        if (Instance != null)
        {
            return Instance;
        }

        var go = new GameObject(objectName);
        DontDestroyOnLoad(go);
        return go.AddComponent<T>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = (T)this;
        OnSingletonAwake();
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        OnSingletonDestroy();
        Instance = null;
    }

    protected virtual void OnSingletonAwake() { }

    protected virtual void OnSingletonDestroy() { }
}
