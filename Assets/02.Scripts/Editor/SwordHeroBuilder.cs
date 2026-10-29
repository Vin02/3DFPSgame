using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// 플레이어를 "Green Armored Figure" 캐릭터로 바꾸고, 오른손에 칼을 쥐여 주고, EEJANAI 칼 애니메이션을 입힌다.
//
// 1) Tools > Characters > Convert Sword Animations
//    EEJANAI 클립은 Unity 기본 근육 범위로 만들어졌는데, 이 프로젝트 플레이어 아바타(Soldier)는 근육 한계가 ±180°라
//    그대로 쓰면 팔다리가 2~3배 과장된다. (메모리: Soldier avatar ±180° limits) 그래서 새 캐릭터 아바타도 Soldier와 같은 한계로 맞추고,
//    EEJANAI 클립은 근육 값을 각도로 풀어 새 한계로 다시 정규화한 사본(Assets/07.Animations/Sword)을 만든다. 원본은 건드리지 않는다.
// 2) Tools > Characters > Build Sword Hero
//    - SoldierLocomotion 컨트롤러를 복사해 SwordLocomotion을 만들고, 상체 레이어를 "칼 자세(상시) + 베기(공격 중)"로 바꾼다.
//    - 씬의 Player/Model(Soldier)을 새 캐릭터로 교체한다. PlayerAnimation 설정은 옮기고, 총알이 나가는 Muzzle은 칼끝에 둔다.
//    - 칼은 오른손 뼈에 붙이되, 손 뼈의 축에서 손가락 방향·엄지 방향을 계산해 악수하듯 칼자루를 쥐는 자세로 맞춘다.
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 저장하지 않는다.
public static class SwordHeroBuilder
{
    private const string CharacterPath = "Assets/05.Models/Green Armored Figure-2bb92956e9c56d6044c379909da6d2d3.fbx";
    private const string SwordPath = "Assets/05.Models/Ornate Green Glowing Sword-c96e6225b7b359b15745d8d95ff7e543.fbx";
    private const string SoldierPath = "Assets/LowPolySoldiers_demo/models/Soldier_demo.FBX";
    private const string SoldierControllerPath = "Assets/LowPolySoldiers_demo/SoldierLocomotion.controller";
    private const string SourceClipFolder = "Assets/EEJANAI_Team/FreeSwordAnimations/Animations/";
    private const string ClipFolder = "Assets/07.Animations/Sword";
    private const string ControllerPath = "Assets/07.Animations/SwordLocomotion.controller";

    private const string IdleClipName = "battle stance";
    // Fire1을 누르고 있는 동안 이 순서대로 이어 베고, 끝나면 처음으로 돌아간다. 제자리에서 이어 보이는 클립만 골랐다.
    private static readonly string[] ComboClipNames = { "slash1", "slash4", "slash7", "slash3" };
    private const float ComboExitTime = 0.8f;       // 클립의 이 지점부터 다음 베기로 넘어간다(뒷동작을 잘라 끊김을 줄인다).
    private const float ComboBlendTime = 0.18f;

    // 새 캐릭터는 키가 0.8(모델 단위)라서 Soldier(머리 높이 약 2.1m)와 비슷해지도록 키운다.
    private const float ModelScale = 2.7f;
    private const float SwordWorldLength = 1.3f;      // 칼 길이(m). 원본 모델 길이는 1.
    private const float PalmOffset = 0.045f;           // 손목 뼈에서 손바닥 중심까지(모델 단위, 손가락 방향)
    // 칼 메시(로컬 좌표): 칼끝 z=-0.5, 손잡이 중심 z≈0.43, 가로날(십자 날밑) x축 방향이 넓다.
    private static readonly Vector3 SwordGripPoint = new Vector3(0f, 0f, 0.43f);
    private static readonly Vector3 SwordTipPoint = new Vector3(0f, 0f, -0.5f);

    // ───────────────────────── 1. 애니메이션 변환 ─────────────────────────

    [MenuItem("Tools/Characters/Convert Sword Animations")]
    private static void ConvertAnimations()
    {
        ApplyHumanLimitsToCharacter();

        EnsureFolder(ClipFolder);
        MuscleRange[] from = DefaultRanges();
        MuscleRange[] to = CharacterRanges();
        string[] names = HumanTrait.MuscleName;

        int converted = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { SourceClipFolder.TrimEnd('/') }))
        {
            string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
            if (!sourcePath.EndsWith(".anim"))
            {
                continue;
            }

            string targetPath = $"{ClipFolder}/{System.IO.Path.GetFileName(sourcePath)}";
            AssetDatabase.DeleteAsset(targetPath);
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                Debug.LogError($"{sourcePath} 복사 실패");
                continue;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
            int curves = 0;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                int muscle = System.Array.IndexOf(names, binding.propertyName);
                if (binding.type != typeof(Animator) || muscle < 0)
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                var rescaled = new AnimationCurve();
                float step = 1f / Mathf.Max(clip.frameRate, 30f);
                for (float t = 0f; t < clip.length + step * 0.5f; t += step)
                {
                    float time = Mathf.Min(t, clip.length);
                    rescaled.AddKey(new Keyframe(time, Retarget(curve.Evaluate(time), from[muscle], to[muscle])));
                }

                for (int i = 0; i < rescaled.length; i++)
                {
                    rescaled.SmoothTangents(i, 0f);
                }

                AnimationUtility.SetEditorCurve(clip, binding, rescaled);
                curves++;
            }

            EditorUtility.SetDirty(clip);
            converted++;
            Debug.Log($"{System.IO.Path.GetFileName(sourcePath)}: 근육 곡선 {curves}개를 새 한계로 변환했습니다.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"EEJANAI 클립 {converted}개를 {ClipFolder}에 변환했습니다.");
    }

    private struct MuscleRange
    {
        public float Min;
        public float Max;
    }

    // 근육 값(-1~1) → 각도 → 새 범위로 다시 정규화
    private static float Retarget(float value, MuscleRange from, MuscleRange to)
    {
        float angle = value >= 0f ? value * from.Max : -value * from.Min;
        if (angle >= 0f)
        {
            return to.Max > 0.0001f ? Mathf.Clamp(angle / to.Max, -1f, 1f) : 0f;
        }

        return to.Min < -0.0001f ? Mathf.Clamp(angle / -to.Min, -1f, 1f) : 0f;
    }

    private static MuscleRange[] DefaultRanges()
    {
        var ranges = new MuscleRange[HumanTrait.MuscleCount];
        for (int i = 0; i < ranges.Length; i++)
        {
            ranges[i] = new MuscleRange { Min = HumanTrait.GetMuscleDefaultMin(i), Max = HumanTrait.GetMuscleDefaultMax(i) };
        }

        return ranges;
    }

    // 새 캐릭터 아바타의 근육 범위. 한계를 직접 정한 뼈는 그 값, 기본값 뼈는 Unity 기본 범위.
    private static MuscleRange[] CharacterRanges()
    {
        var limits = new Dictionary<string, HumanLimit>();
        foreach (HumanBone bone in ((ModelImporter)AssetImporter.GetAtPath(CharacterPath)).humanDescription.human)
        {
            limits[bone.humanName] = bone.limit;
        }

        MuscleRange[] ranges = DefaultRanges();
        for (int bone = 0; bone < HumanTrait.BoneCount; bone++)
        {
            if (!limits.TryGetValue(HumanTrait.BoneName[bone], out HumanLimit limit) || limit.useDefaultValues)
            {
                continue;
            }

            for (int dof = 0; dof < 3; dof++)
            {
                int muscle = HumanTrait.MuscleFromBone(bone, dof);
                if (muscle >= 0)
                {
                    ranges[muscle] = new MuscleRange { Min = limit.min[dof], Max = limit.max[dof] };
                }
            }
        }

        return ranges;
    }

    // 새 캐릭터를 Humanoid로 만들고, 뼈마다 Soldier 아바타와 같은 근육 한계를 준다.
    private static void ApplyHumanLimitsToCharacter()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(CharacterPath);
        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            importer = (ModelImporter)AssetImporter.GetAtPath(CharacterPath);
            changed = true;
        }

        HumanDescription soldier = ((ModelImporter)AssetImporter.GetAtPath(SoldierPath)).humanDescription;
        var soldierLimits = new Dictionary<string, HumanLimit>();
        foreach (HumanBone bone in soldier.human)
        {
            soldierLimits[bone.humanName] = bone.limit;
        }

        HumanDescription description = importer.humanDescription;
        HumanBone[] bones = description.human;
        for (int i = 0; i < bones.Length; i++)
        {
            if (soldierLimits.TryGetValue(bones[i].humanName, out HumanLimit limit) && !LimitsEqual(bones[i].limit, limit))
            {
                bones[i].limit = limit;
                changed = true;
            }
        }

        if (changed)
        {
            description.human = bones;
            description.upperArmTwist = soldier.upperArmTwist;
            description.lowerArmTwist = soldier.lowerArmTwist;
            description.upperLegTwist = soldier.upperLegTwist;
            description.lowerLegTwist = soldier.lowerLegTwist;
            description.armStretch = soldier.armStretch;
            description.legStretch = soldier.legStretch;
            description.feetSpacing = soldier.feetSpacing;
            importer.humanDescription = description;
            importer.SaveAndReimport();
        }
    }

    private static bool LimitsEqual(HumanLimit a, HumanLimit b)
    {
        return a.useDefaultValues == b.useDefaultValues && a.min == b.min && a.max == b.max && a.center == b.center;
    }

    // ───────────────────────── 2. 컨트롤러 ─────────────────────────

    private static AnimatorController BuildController()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        if (!AssetDatabase.CopyAsset(SoldierControllerPath, ControllerPath))
        {
            Debug.LogError("SoldierLocomotion 컨트롤러를 복사하지 못했습니다.");
            return null;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var stance = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{IdleClipName}.anim");
        if (stance == null || !ApplyCombo(controller))
        {
            Debug.LogError("변환된 칼 애니메이션이 없습니다. Tools > Characters > Convert Sword Animations를 먼저 실행하세요.");
            return null;
        }

        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name != "UpperBody")
            {
                continue;
            }

            foreach (ChildAnimatorState child in layer.stateMachine.states)
            {
                // None(기본, 평소) = 칼 자세, Shoot(Fire1을 누르는 동안) = 베기. 상체 마스크라 다리는 아래 레이어의 달리기를 그대로 쓴다.
                if (child.state.name == "None")
                {
                    child.state.motion = stance;
                }
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // UpperBody 레이어의 Shoot 상태를 "ComboClipNames 순서로 이어지는 상태 사슬"로 바꾼다. 여러 번 실행해도 같은 결과가 된다.
    // None → Shoot(첫 베기) → Combo2 → Combo3 … → 다시 Shoot. 어느 베기에서든 Shoot를 떼면 None(칼 자세)로 돌아간다.
    private static bool ApplyCombo(AnimatorController controller)
    {
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            if (layer.name != "UpperBody")
            {
                continue;
            }

            AnimatorStateMachine machine = layer.stateMachine;
            AnimatorState first = null;
            AnimatorState none = null;
            Vector3 firstPosition = Vector3.zero;
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == "Shoot")
                {
                    first = child.state;
                    firstPosition = child.position;
                }
                else if (child.state.name == "None")
                {
                    none = child.state;
                }
            }

            if (first == null || none == null)
            {
                Debug.LogError("UpperBody 레이어에 None/Shoot 상태가 없습니다.");
                return false;
            }

            // 이전에 만든 이어베기 상태는 지우고 다시 만든다.
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name.StartsWith("Combo"))
                {
                    machine.RemoveState(child.state);
                }
            }

            var chain = new List<AnimatorState> { first };
            for (int i = 1; i < ComboClipNames.Length; i++)
            {
                AnimatorState state = machine.AddState($"Combo{i + 1}", firstPosition + new Vector3(0f, 60f * i, 0f));
                chain.Add(state);
            }

            for (int i = 0; i < chain.Count; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{ComboClipNames[i]}.anim");
                if (clip == null)
                {
                    Debug.LogError($"칼 애니메이션 {ComboClipNames[i]}가 없습니다. Tools > Characters > Convert Sword Animations를 먼저 실행하세요.");
                    return false;
                }

                chain[i].motion = clip;
                chain[i].speed = 1f;

                // 기존 전환은 모두 지우고: 베기 끝 무렵 다음 베기로, Shoot를 떼면 칼 자세로.
                foreach (AnimatorStateTransition old in chain[i].transitions)
                {
                    chain[i].RemoveTransition(old);
                }

                AnimatorStateTransition next = chain[i].AddTransition(chain[(i + 1) % chain.Count]);
                next.hasExitTime = true;
                next.exitTime = ComboExitTime;
                next.duration = ComboBlendTime;
                next.hasFixedDuration = true;
                next.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");

                AnimatorStateTransition release = chain[i].AddTransition(none);
                release.hasExitTime = false;
                release.duration = 0.15f;
                release.hasFixedDuration = true;
                release.AddCondition(AnimatorConditionMode.IfNot, 0f, "Shoot");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return true;
        }

        Debug.LogError("UpperBody 레이어를 찾지 못했습니다.");
        return false;
    }

    // 컨트롤러를 새로 만들지 않고, 지금 컨트롤러의 베기만 이어베기로 바꾼다. 씬은 건드리지 않는다.
    [MenuItem("Tools/Characters/Apply Sword Combo")]
    private static void ApplyComboToExisting()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("SwordLocomotion 컨트롤러가 없습니다. Tools > Characters > Build Sword Hero를 먼저 실행하세요.");
            return;
        }

        if (ApplyCombo(controller))
        {
            Debug.Log($"이어베기 {ComboClipNames.Length}연: {string.Join(" → ", ComboClipNames)}");
        }
    }

    // ───────────────────────── 3. 플레이어 교체 ─────────────────────────

    [MenuItem("Tools/Characters/Build Sword Hero")]
    private static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        GameObject player = GameObject.Find("Player");
        Transform oldModel = player != null ? player.transform.Find("Model") : null;
        var characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPath);
        var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
        if (player == null || oldModel == null || characterPrefab == null || swordPrefab == null)
        {
            Debug.LogError("Player/Model, 캐릭터 또는 칼 모델을 찾지 못했습니다.");
            return;
        }

        ApplyHumanLimitsToCharacter();
        AnimatorController controller = BuildController();
        if (controller == null)
        {
            return;
        }

        Undo.SetCurrentGroupName("Build Sword Hero");
        int undoGroup = Undo.GetCurrentGroup();

        PlayerAnimation oldAnimation = oldModel.GetComponent<PlayerAnimation>();

        // 새 캐릭터: 이름은 Model, 위치는 옛 모델과 같은 곳(발이 땅에 닿는 자리)
        var model = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
        Undo.RegisterCreatedObjectUndo(model, "Build Sword Hero");
        model.name = "Model";
        model.transform.SetParent(player.transform, false);
        model.transform.localPosition = oldModel.localPosition;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * ModelScale;

        var animator = model.GetComponent<Animator>();
        if (animator == null)
        {
            animator = model.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        if (animator.avatar == null || !animator.isHuman)
        {
            Debug.LogError("새 캐릭터의 Humanoid 아바타가 올바르지 않습니다.");
            return;
        }

        // 머리·얼굴이 카메라를 가리지 않게 그림자는 유지하되 보이는 설정은 그대로 둔다.
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.receiveShadows = true;
        }

        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform sword = AttachSword(model.transform, hand, lowerArm, swordPrefab);
        Transform muzzle = CreateMuzzle(hand, sword);

        // PlayerAnimation: 설정은 그대로 옮기고, 옛 뼈 참조는 새 것으로 바꾼다.
        var animation = model.GetComponent<PlayerAnimation>();
        if (animation == null)
        {
            animation = model.AddComponent<PlayerAnimation>();
        }

        if (oldAnimation != null)
        {
            EditorUtility.CopySerialized(oldAnimation, animation);
        }

        var animationSerialized = new SerializedObject(animation);
        animationSerialized.FindProperty("_animator").objectReferenceValue = animator;
        // 사격 때 총열을 정면으로 돌리는 보정은 칼에는 필요 없으므로 끈다. (참조를 비우면 꺼진다)
        animationSerialized.FindProperty("_spine").objectReferenceValue = null;
        animationSerialized.FindProperty("_hand").objectReferenceValue = null;
        animationSerialized.FindProperty("_muzzle").objectReferenceValue = null;
        animationSerialized.ApplyModifiedPropertiesWithoutUndo();

        RewireReferences(oldModel, oldAnimation, animation, muzzle);

        Undo.DestroyObjectImmediate(oldModel.gameObject);

        // 새 모델을 옛 모델이 있던 자리(자식 순서)로
        EditorSceneManager.MarkSceneDirty(player.scene);
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log("플레이어를 새 캐릭터로 바꾸고 오른손에 칼을 쥐였습니다. 결과를 확인한 뒤 씬을 저장하세요.");
    }

    // 손 뼈에 칼을 붙인다. 손가락 방향 = 날밑이 뻗은 방향, 엄지 방향 = 칼끝이 향하는 방향(악수하듯 쥔 자세).
    private static Transform AttachSword(Transform root, Transform hand, Transform lowerArm, GameObject swordPrefab)
    {
        // 기본(바인드) 자세에서 손 뼈 로컬 축을 구한다. 팔이 아래로 늘어진 자세라 손가락 방향 = 팔꿈치→손, 엄지는 앞쪽(+Z)이다.
        Vector3 fingerWorld = (hand.position - lowerArm.position).normalized;
        Vector3 thumbWorld = Vector3.ProjectOnPlane(root.forward, fingerWorld).normalized;
        Vector3 fingerLocal = hand.InverseTransformDirection(fingerWorld);
        Vector3 bladeLocal = hand.InverseTransformDirection(thumbWorld);

        Quaternion target = Quaternion.LookRotation(bladeLocal, fingerLocal);               // z → 칼끝 방향, y → 날밑 방향
        Quaternion meshFrame = Quaternion.LookRotation(Vector3.back, Vector3.right);         // 메시의 칼끝(-z)과 날밑(x)
        Quaternion rotation = target * Quaternion.Inverse(meshFrame);

        float scale = SwordWorldLength / hand.lossyScale.x;
        Vector3 palm = fingerLocal * PalmOffset;
        Vector3 position = palm - rotation * (SwordGripPoint * scale);

        Transform existing = hand.Find("Sword");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var sword = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab);
        Undo.RegisterCreatedObjectUndo(sword, "Build Sword Hero");
        sword.name = "Sword";
        sword.transform.SetParent(hand, false);
        sword.transform.localPosition = position;
        sword.transform.localRotation = rotation;
        sword.transform.localScale = Vector3.one * scale;
        return sword.transform;
    }

    // 총알이 나가는 곳은 칼끝이다. 손 뼈의 자식으로 두고(크기 1을 유지해 머즐 플래시가 커지지 않게) 위치만 칼끝에 맞춘다.
    private static Transform CreateMuzzle(Transform hand, Transform sword)
    {
        Transform existing = hand.Find("Muzzle");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var muzzle = new GameObject("Muzzle");
        Undo.RegisterCreatedObjectUndo(muzzle, "Build Sword Hero");
        muzzle.transform.SetParent(hand, false);
        muzzle.transform.position = sword.TransformPoint(SwordTipPoint);
        muzzle.transform.localRotation = Quaternion.identity;
        muzzle.transform.localScale = Vector3.one / hand.lossyScale.x;
        return muzzle.transform;
    }

    // 씬의 다른 컴포넌트가 옛 모델(PlayerAnimation, 손의 Muzzle 등)을 가리키고 있으면 새 것으로 바꾼다.
    private static void RewireReferences(Transform oldModel, PlayerAnimation oldAnimation, PlayerAnimation newAnimation, Transform newMuzzle)
    {
        foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour.transform.IsChildOf(oldModel))
            {
                continue;
            }

            var serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                {
                    continue;
                }

                Object value = property.objectReferenceValue;
                if (value == oldAnimation)
                {
                    property.objectReferenceValue = newAnimation;
                    changed = true;
                }
                else if (value is Transform transform && transform.IsChildOf(oldModel))
                {
                    if (transform.name == "Muzzle")
                    {
                        property.objectReferenceValue = newMuzzle;
                        changed = true;
                    }
                    else
                    {
                        Debug.LogWarning($"{behaviour.name}.{property.propertyPath}가 옛 모델의 {transform.name}을 가리키고 있어 비웁니다.");
                        property.objectReferenceValue = null;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
