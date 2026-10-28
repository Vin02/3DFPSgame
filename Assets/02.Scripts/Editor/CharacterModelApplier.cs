using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// 플레이어를 LowPolySoldier로, 씬의 좀비를 Zombie 에셋 모델로 교체한다. 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다.
// 씬 파일은 저장하지 않는다. 결과를 확인한 뒤 직접 저장한다. (Ctrl+Z로 되돌릴 수 있다)
public static class CharacterModelApplier
{
    private const string ModelName = "Model";
    private const string PlayerName = "Player";
    private const string SoldierPrefabPath = "Assets/LowPolySoldiers_demo/Soldier_demo_Prefab.prefab";
    private const string SoldierAnimDir = "Assets/LowPolySoldiers_demo/animation/";
    private const string SoldierControllerPath = "Assets/LowPolySoldiers_demo/SoldierLocomotion.controller";
    private const string UpperBodyMaskPath = "Assets/LowPolySoldiers_demo/SoldierUpperBody.mask";
    private const string UpperBodyLayerName = "UpperBody";
    private const string InjuredLayerName = "Injured";
    private const string LowerBodyMaskPath = "Assets/LowPolySoldiers_demo/SoldierLowerBody.mask";
    private const string InjuredFbxPath = "Assets/07.Animations/Ely By K.Atienza@Injured Run.fbx";
    private const string RightHandBoneName = "Bip001 R Hand";
    private const string SpineBoneName = "Bip001 Spine";
    private const string MuzzleName = "Muzzle";
    // 조준 자세에서 총 메시 정점을 계산해 구한 총열 끝 위치 (오른손 본 로컬)
    private static readonly Vector3 MuzzleLocalPosition = new Vector3(-1.03f, 0.04f, 0.14f);
    private const string ZombieAnimDir = "Assets/Zombie/Animations/";
    private const string ZombieControllerPath = "Assets/Zombie/Animations/ZombieLocomotion.controller";
    private static readonly string[] ZombiePrefabPaths =
    {
        "Assets/Zombie/Prefabs/Zombie1.prefab",
        "Assets/Zombie/Prefabs/Zombie2.prefab",
        "Assets/Zombie/Prefabs/Zombie3.prefab",
    };

    [MenuItem("Tools/Characters/Apply Soldier And Zombie Models")]
    private static void Apply()
    {
        Undo.SetCurrentGroupName("Apply Character Models");
        int undoGroup = Undo.GetCurrentGroup();

        ApplyPlayer();
        ApplyZombies();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkAllScenesDirty();
    }

    private static void ApplyPlayer()
    {
        GameObject player = GameObject.Find(PlayerName);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPrefabPath);

        if (player == null || prefab == null)
        {
            Debug.LogError("플레이어 또는 Soldier 프리팹을 찾지 못했습니다.");
            return;
        }

        Transform model = FindOrCreateModel(player.transform, prefab);

        // 닭 모양 프리미티브는 삭제하지 않고 렌더러만 끈다. (WingFlapper 등 기존 참조 유지)
        foreach (MeshRenderer meshRenderer in player.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!meshRenderer.transform.IsChildOf(model))
            {
                Undo.RecordObject(meshRenderer, "Hide chicken");
                meshRenderer.enabled = false;
            }
        }

        Animator animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = BuildSoldierController();
        animator.applyRootMotion = false;
        GetOrAdd<PlayerAnimation>(model.gameObject);

        AssignMuzzle(player, model);
    }

    // 총구 위치를 오른손 본의 자식으로 만들어 두면, 어떤 애니메이션에서도 총을 따라간다.
    private static void AssignMuzzle(GameObject player, Transform model)
    {
        Transform hand = FindDescendant(model, RightHandBoneName);
        var gunFire = player.GetComponent<PlayerGunFire>();

        if (hand == null || gunFire == null)
        {
            Debug.LogError("오른손 본 또는 PlayerGunFire를 찾지 못했습니다.");
            return;
        }

        Transform muzzle = hand.Find(MuzzleName);
        if (muzzle == null)
        {
            var muzzleObject = new GameObject(MuzzleName);
            Undo.RegisterCreatedObjectUndo(muzzleObject, "Create muzzle");
            muzzle = muzzleObject.transform;
            muzzle.SetParent(hand, false);
        }

        muzzle.localPosition = MuzzleLocalPosition;

        var serialized = new SerializedObject(gunFire);
        serialized.FindProperty("_muzzle").objectReferenceValue = muzzle;
        serialized.ApplyModifiedProperties();

        // 사격 중 총열을 정면으로 맞추는 보정에 쓸 참조
        var animation = model.GetComponent<PlayerAnimation>();
        var animationSerialized = new SerializedObject(animation);
        animationSerialized.FindProperty("_spine").objectReferenceValue = FindDescendant(model, SpineBoneName);
        animationSerialized.FindProperty("_hand").objectReferenceValue = hand;
        animationSerialized.FindProperty("_muzzle").objectReferenceValue = muzzle;
        animationSerialized.FindProperty("_health").objectReferenceValue = GetOrAdd<PlayerHealth>(player);
        animationSerialized.ApplyModifiedProperties();
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static void ApplyZombies()
    {
        GameObject[] zombies = FindSceneZombies();
        RuntimeAnimatorController controller = BuildZombieController();

        for (int i = 0; i < zombies.Length; i++)
        {
            GameObject root = zombies[i];
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombiePrefabPaths[i % ZombiePrefabPaths.Length]);

            // 프리미티브 몸체(Body)와 절차적 애니메이션은 제거하고 렌더러만 끈다.
            Transform body = root.transform.Find("Body");
            if (body != null)
            {
                Undo.DestroyObjectImmediate(body.gameObject);
            }

            MeshRenderer rootRenderer = root.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
            {
                Undo.RecordObject(rootRenderer, "Hide zombie root");
                rootRenderer.enabled = false;
            }

            Transform model = FindOrCreateModel(root.transform, prefab);
            Animator animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            GetOrAdd<ZombieAnimation>(model.gameObject);
        }
    }

    private static GameObject[] FindSceneZombies()
    {
        List<GameObject> result = new List<GameObject>();
        foreach (Zombie zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.InstanceID))
        {
            result.Add(zombie.gameObject);
        }

        return result.ToArray();
    }

    private static Transform FindOrCreateModel(Transform parent, GameObject prefab)
    {
        Transform model = parent.Find(ModelName);
        if (model != null)
        {
            return model;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, "Create model");
        instance.name = ModelName;

        // 캐릭터 컨트롤러 캡슐 바닥에 발이 오도록 맞춘다.
        CharacterController controller = parent.GetComponent<CharacterController>();
        float footOffset = controller != null ? controller.center.y - controller.height * 0.5f : 0f;
        instance.transform.localPosition = new Vector3(0f, footOffset, 0f);
        instance.transform.localRotation = Quaternion.identity;

        if (instance.GetComponent<Animator>() == null)
        {
            Undo.AddComponent<Animator>(instance);
        }

        return instance.transform;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static AnimatorController BuildSoldierController()
    {
        AnimatorController controller = CreateController(SoldierControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimationClip idle = LoadClip(SoldierAnimDir + "demo_combat_idle.FBX");
        AnimationClip run = LoadClip(SoldierAnimDir + "demo_combat_run.FBX");
        AnimationClip shoot = LoadClip(SoldierAnimDir + "demo_combat_shoot.FBX");

        AnimatorState idleState = machine.AddState("Idle");
        idleState.motion = idle;
        AnimatorState runState = machine.AddState("Run");
        runState.motion = run;
        machine.defaultState = idleState;

        AddTransition(idleState, runState, "Speed", AnimatorConditionMode.Greater, 0.5f);
        AddTransition(runState, idleState, "Speed", AnimatorConditionMode.Less, 0.5f);

        BuildUpperBodyShootLayer(controller, shoot);
        BuildInjuredLayer(controller, LoadInjuredClip());

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // 상체 전용 레이어: 사격 중에는 상체만 사격 모션으로 덮어쓰고, 하체는 기본 레이어(Idle/Run)를 그대로 따른다.
    private static void BuildUpperBodyShootLayer(AnimatorController controller, AnimationClip shoot)
    {
        controller.AddLayer(UpperBodyLayerName);

        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer layer = layers[layers.Length - 1];
        layer.avatarMask = FindOrCreateUpperBodyMask();
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.defaultWeight = 1f;
        controller.layers = layers;

        AnimatorStateMachine machine = layer.stateMachine;
        AnimatorState none = machine.AddState("None");
        AnimatorState shootState = machine.AddState("Shoot");
        shootState.motion = shoot;
        machine.defaultState = none;

        AddBoolTransition(none, shootState, "Shoot", true);
        AddBoolTransition(shootState, none, "Shoot", false);

        // 사격 클립은 루프가 아니므로, 발사 버튼을 누르고 있는 동안 끝나면 처음부터 다시 재생한다.
        AnimatorStateTransition repeat = shootState.AddTransition(shootState);
        repeat.hasExitTime = true;
        repeat.exitTime = 1f;
        repeat.duration = 0f;
        repeat.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
    }

    // 부상 레이어: 체력이 절반 이하일 때(레이어 가중치 1) 이동 중이면 하체만 절뚝이는 달리기로 덮어쓴다.
    // 가중치는 PlayerAnimation이 체력에 맞춰 조절한다. 상체는 사격 레이어를 그대로 쓴다.
    private static void BuildInjuredLayer(AnimatorController controller, AnimationClip injuredRun)
    {
        controller.AddLayer(InjuredLayerName);

        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer layer = layers[layers.Length - 1];
        layer.avatarMask = FindOrCreateLowerBodyMask();
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.defaultWeight = 0f;
        controller.layers = layers;

        AnimatorStateMachine machine = layer.stateMachine;
        AnimatorState none = machine.AddState("None");
        AnimatorState injured = machine.AddState("InjuredRun");
        injured.motion = injuredRun;
        machine.defaultState = none;

        AddTransition(none, injured, "Speed", AnimatorConditionMode.Greater, 0.5f);
        AddTransition(injured, none, "Speed", AnimatorConditionMode.Less, 0.5f);
    }

    // Injured Run은 루프가 아니고 루트 이동이 섞여 있을 수 있어, 제자리 반복 클립으로 임포트 설정을 맞춘다.
    private static AnimationClip LoadInjuredClip()
    {
        var importer = AssetImporter.GetAtPath(InjuredFbxPath) as ModelImporter;

        if (importer != null)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;

            bool changed = false;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].loopTime && clips[i].lockRootRotation && clips[i].lockRootPositionXZ)
                {
                    continue;
                }

                clips[i].loopTime = true;
                clips[i].lockRootRotation = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].lockRootHeightY = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionXZ = true;
                clips[i].keepOriginalPositionY = true;
                changed = true;
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        return LoadClip(InjuredFbxPath);
    }

    // Humanoid 마스크: 루트와 양다리(발 IK 포함)만 켠다.
    private static AvatarMask FindOrCreateLowerBodyMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(LowerBodyMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, LowerBodyMaskPath);
        }

        foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
        {
            if (part == AvatarMaskBodyPart.LastBodyPart)
            {
                continue;
            }

            bool isLower = part == AvatarMaskBodyPart.Root
                || part == AvatarMaskBodyPart.LeftLeg
                || part == AvatarMaskBodyPart.RightLeg
                || part == AvatarMaskBodyPart.LeftFootIK
                || part == AvatarMaskBodyPart.RightFootIK;

            mask.SetHumanoidBodyPartActive(part, isLower);
        }

        EditorUtility.SetDirty(mask);
        return mask;
    }

    // Humanoid 마스크: 몸통·머리·양팔·손가락만 켜고, 루트와 다리는 끈다.
    private static AvatarMask FindOrCreateUpperBodyMask()
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
        }

        foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
        {
            if (part == AvatarMaskBodyPart.LastBodyPart)
            {
                continue;
            }

            bool isUpper = part == AvatarMaskBodyPart.Body
                || part == AvatarMaskBodyPart.Head
                || part == AvatarMaskBodyPart.LeftArm
                || part == AvatarMaskBodyPart.RightArm
                || part == AvatarMaskBodyPart.LeftFingers
                || part == AvatarMaskBodyPart.RightFingers;

            mask.SetHumanoidBodyPartActive(part, isUpper);
        }

        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static AnimatorController BuildZombieController()
    {
        AnimatorController controller = CreateController(ZombieControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState idle = machine.AddState("Idle");
        idle.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ZombieAnimDir + "Z_Idle.anim");
        AnimatorState walk = machine.AddState("Walk");
        walk.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ZombieAnimDir + "Z_Walk_InPlace.anim");
        AnimatorState run = machine.AddState("Run");
        run.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ZombieAnimDir + "Z_Run_InPlace.anim");
        AnimatorState attack = machine.AddState("Attack");
        attack.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ZombieAnimDir + "Z_Attack.anim");
        AnimatorState dead = machine.AddState("Dead");
        dead.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ZombieAnimDir + "Z_FallingBack.anim");
        machine.defaultState = idle;

        // 순찰 속도 2, 추적 속도 5
        AddTransition(idle, walk, "Speed", AnimatorConditionMode.Greater, 0.3f);
        AddTransition(walk, idle, "Speed", AnimatorConditionMode.Less, 0.3f);
        AddTransition(walk, run, "Speed", AnimatorConditionMode.Greater, 3.5f);
        AddTransition(run, walk, "Speed", AnimatorConditionMode.Less, 3.5f);
        foreach (AnimatorState from in new[] { idle, walk, run })
        {
            AddBoolTransition(from, attack, "Attack", true);
        }

        AddBoolTransition(attack, idle, "Attack", false);

        // 어느 상태에서든 죽으면 쓰러지고, 풀에서 다시 꺼내 Dead가 꺼지면 Idle로 돌아간다.
        foreach (AnimatorState from in new[] { idle, walk, run, attack })
        {
            AddBoolTransition(from, dead, "Dead", true);
        }

        AddBoolTransition(dead, idle, "Dead", false);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // 이미 있으면 상태·파라미터를 비우고 다시 채운다.
    private static AnimatorController CreateController(string path)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        for (int i = controller.layers.Length - 1; i > 0; i--)
        {
            controller.RemoveLayer(i);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states)
        {
            machine.RemoveState(child.state);
        }

        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            controller.RemoveParameter(parameter);
        }

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Shoot", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        return controller;
    }

    private static AnimationClip LoadClip(string fbxPath)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }

        Debug.LogError($"애니메이션 클립을 찾지 못했습니다: {fbxPath}");
        return null;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.15f;
        transition.AddCondition(mode, threshold, parameter);
    }

    private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.1f;
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
    }
}
