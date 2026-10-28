using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 왼팔 오버핸드 투척 클립을 Humanoid 근육 키프레임으로 만들고, 플레이어 컨트롤러에 왼팔만 덮는 Throw 레이어로 연결한다.
// 근육 값은 관절별 가동 범위 안으로 제한되므로 팔이 사람이 할 수 없는 방향으로 꺾이지 않는다.
// (외부 Grenade Throw 클립은 상체를 크게 비틀며 던지는 전신 동작이라, 팔만 쓰면 팔이 등 뒤로 꺾여 보여 쓰지 않는다)
// 이름으로 찾아 갱신하므로 다시 실행해도 중복되지 않는다. 씬 파일은 건드리지 않는다.
//
// 클립 정규화 시간: 0 = 살짝 젖힘, 0.2 = 최대로 젖힘(차지 끝), 0.45 = 앞으로 뻗음(놓는 순간), 0.7 = 마무리.
// PlayerAnimation의 Throw 구간 값이 이 시간에 맞춰져 있다.
public static class ThrowArmClipBuilder
{
    private const string ControllerPath = "Assets/LowPolySoldiers_demo/SoldierLocomotion.controller";
    private const string ClipPath = "Assets/07.Animations/Throw_LeftArm.anim";
    private const string MaskPath = "Assets/07.Animations/Throw_LeftArm.mask";

    private const string ThrowLayerName = "Throw";
    private const string ThrowTimeParameter = "ThrowTime";

    // 이전 방식(Grenade Throw 클립)이 만든 에셋과 레이어. 다시 실행할 때 정리한다.
    private static readonly string[] StaleAssetPaths =
    {
        "Assets/07.Animations/GrenadeThrow_LeftArm.mask",
        "Assets/07.Animations/GrenadeThrow_Body.mask",
        "Assets/07.Animations/GrenadeThrow_Soldier.anim",
    };
    private const string StaleBodyLayerName = "ThrowBody";

    private static readonly float[] KeyTimes = { 0f, 0.2f, 0.45f, 0.7f, 1f };

    // 근육 이름별 키 값(KeyTimes 순서).
    // Soldier 아바타는 팔 관절 범위가 ±180°로 설정돼 있어 근육 1단위가 약 180°다(Unity 기본 범위가 아님).
    // 그래서 값이 작다. 팔 값은 목표 방향(모델 기준 윗팔·팔뚝 방향)을 내도록 실제 캐릭터에서 풀어 구했다.
    //   살짝 젖힘: 윗팔 왼쪽 수평, 팔뚝 위로 (팔꿈치 약 75°)
    //   최대 젖힘: 윗팔 왼쪽 위 뒤로, 팔뚝 위로 (손이 귀 뒤, 팔꿈치 약 60°)
    //   놓는 순간: 팔이 앞 위로 거의 펴짐
    //   마무리:   팔이 앞 아래로
    // 어깨(쇄골)·팔뚝 비틀기·손목은 총 든 대기 자세 값을 그대로 둔다.
    private static readonly (string muscle, float[] values)[] Keys =
    {
        ("Left Shoulder Down-Up",     new[] { -0.023f, -0.023f, -0.023f, -0.023f, -0.023f }),
        ("Left Shoulder Front-Back",  new[] { 0.068f,  0.068f,  0.068f,  0.068f,  0.068f }),
        ("Left Arm Down-Up",          new[] { 0.231f,  0.215f,  0.397f,  0.149f,  0.149f }),
        ("Left Arm Front-Back",       new[] { 0.403f,  0.558f,  -0.065f, -0.159f, -0.159f }),
        ("Left Arm Twist In-Out",     new[] { 0.557f,  0.743f,  0f,      0f,      0f }),
        ("Left Forearm Stretch",      new[] { 0.023f,  0.123f,  0.396f,  0.396f,  0.396f }),
        ("Left Forearm Twist In-Out", new[] { 0.611f,  0.611f,  0.611f,  0.611f,  0.611f }),
        ("Left Hand Down-Up",         new[] { -0.086f, -0.086f, -0.086f, -0.086f, -0.086f }),
        ("Left Hand In-Out",          new[] { 0.078f,  0.078f,  0.078f,  0.078f,  0.078f }),
    };

    [MenuItem("Tools/Characters/Build Left Arm Throw")]
    private static void Build()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"컨트롤러를 찾지 못했습니다: {ControllerPath}");
            return;
        }

        AnimationClip clip = SaveClip();
        AvatarMask mask = SaveMask();

        RemoveLayer(controller, StaleBodyLayerName);
        EnsureThrowTimeParameter(controller);
        SetupThrowLayer(controller, clip, mask);

        foreach (string path in StaleAssetPaths)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"왼팔 투척 클립 적용 완료: {ClipPath}, 컨트롤러 '{ThrowLayerName}' 레이어");
    }

    private static AnimationClip SaveClip()
    {
        var clip = new AnimationClip { name = "Throw_LeftArm", frameRate = 30f };

        foreach ((string muscle, float[] values) in Keys)
        {
            var curve = new AnimationCurve();
            for (int i = 0; i < KeyTimes.Length; i++)
            {
                curve.AddKey(KeyTimes[i], values[i]);
            }

            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Auto);
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle), curve);
        }

        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(clip, ClipPath);
            return clip;
        }

        EditorUtility.CopySerialized(clip, existing);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // 왼팔(어깨·팔·손)만 덮는다. 손가락은 아래 레이어의 쥐는 자세를 유지한다.
    private static AvatarMask SaveMask()
    {
        var mask = new AvatarMask { name = "Throw_LeftArm" };
        foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
        {
            if (part == AvatarMaskBodyPart.LastBodyPart)
            {
                continue;
            }

            mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.LeftArm);
        }

        var existing = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mask, MaskPath);
            return mask;
        }

        EditorUtility.CopySerialized(mask, existing);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void RemoveLayer(AnimatorController controller, string layerName)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == layerName)
            {
                controller.RemoveLayer(i);
                return;
            }
        }
    }

    private static void EnsureThrowTimeParameter(AnimatorController controller)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == ThrowTimeParameter)
            {
                return;
            }
        }

        controller.AddParameter(ThrowTimeParameter, AnimatorControllerParameterType.Float);
    }

    private static void SetupThrowLayer(AnimatorController controller, AnimationClip clip, AvatarMask mask)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        int index = -1;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == ThrowLayerName)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            controller.AddLayer(ThrowLayerName);
            layers = controller.layers;
            index = layers.Length - 1;
        }

        AnimatorControllerLayer layer = layers[index];
        layer.avatarMask = mask;
        layer.defaultWeight = 0f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;

        AnimatorStateMachine machine = layer.stateMachine;
        AnimatorState state = null;
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == ThrowLayerName)
            {
                state = child.state;
                break;
            }
        }

        if (state == null)
        {
            state = machine.AddState(ThrowLayerName);
        }

        state.motion = clip;
        state.speed = 0f;
        state.mirror = false;
        state.timeParameterActive = true;
        state.timeParameter = ThrowTimeParameter;
        state.writeDefaultValues = true;
        machine.defaultState = state;

        controller.layers = layers;
        EditorUtility.SetDirty(controller);
    }
}
