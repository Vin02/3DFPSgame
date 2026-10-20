using System;
using UnityEngine;

// 한 프레임 동안 수직 움직임 계산에 필요한 입력과 상태
public readonly struct VerticalMotionInput
{
    public readonly bool IsGrounded;
    public readonly bool HitCeiling;
    public readonly bool JumpPressed;
    public readonly bool JumpReleased;

    public VerticalMotionInput(bool isGrounded, bool hitCeiling, bool jumpPressed, bool jumpReleased)
    {
        IsGrounded = isGrounded;
        HitCeiling = hitCeiling;
        JumpPressed = jumpPressed;
        JumpReleased = jumpReleased;
    }
}

// 점프, 중력, 낙하 같은 수직 방향 움직임을 계산한다.
// Unity 입력과 이동 적용은 PlayerMove가 맡고, 여기서는 수직 속도만 돌려준다.
[Serializable]
public class PlayerVerticalMotion
{
    private const float GroundedStickVelocity = -2f;

    [Header("Jump")]
    [SerializeField] private float _jumpPower = 10f;
    [SerializeField] private int _maxJumpCount = 2;
    [Tooltip("점프 키를 일찍 뗐을 때 남기는 상승 속도 비율")]
    [SerializeField, Range(0f, 1f)] private float _jumpCutMultiplier = 0.5f;

    [Header("Forgiveness")]
    [Tooltip("땅에서 떨어진 직후에도 점프를 허용하는 시간(초)")]
    [SerializeField] private float _coyoteTime = 0.1f;
    [Tooltip("착지 직전에 누른 점프를 기억하는 시간(초)")]
    [SerializeField] private float _jumpBufferTime = 0.1f;

    [Header("Gravity")]
    [SerializeField] private float _gravity = -20f;
    [SerializeField] private float _fallGravityMultiplier = 1.8f;
    [SerializeField] private float _maxFallSpeed = 25f;

    private float _yVelocity;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private int _jumpsRemaining;

    // 땅 위에 있거나, 땅에서 막 떨어진 직후(코요테 시간)인가
    public bool HasGroundContact => _coyoteTimer > 0f;

    public float Tick(VerticalMotionInput input, float deltaTime)
    {
        UpdateGroundContact(input.IsGrounded, deltaTime);
        UpdateJumpBuffer(input.JumpPressed, deltaTime);

        ApplyGravity(input.IsGrounded, deltaTime);
        StopAtCeiling(input.HitCeiling);
        TryJump();
        CutJumpShort(input.JumpReleased);
        ClampFallSpeed();

        return _yVelocity;
    }

    private void UpdateGroundContact(bool isGrounded, float deltaTime)
    {
        if (isGrounded)
        {
            _coyoteTimer = _coyoteTime;
            _jumpsRemaining = _maxJumpCount;
            return;
        }

        _coyoteTimer -= deltaTime;

        // 점프하지 않고 떨어졌다면 땅에서의 첫 점프 기회는 사라진 것으로 센다.
        if (_coyoteTimer <= 0f && _jumpsRemaining == _maxJumpCount)
        {
            _jumpsRemaining = _maxJumpCount - 1;
        }
    }

    private void UpdateJumpBuffer(bool jumpPressed, float deltaTime)
    {
        if (jumpPressed)
        {
            _jumpBufferTimer = _jumpBufferTime;
            return;
        }

        _jumpBufferTimer -= deltaTime;
    }

    private void ApplyGravity(bool isGrounded, float deltaTime)
    {
        if (isGrounded && _yVelocity <= 0f)
        {
            _yVelocity = GroundedStickVelocity;
            return;
        }

        float gravityScale = _yVelocity < 0f ? _fallGravityMultiplier : 1f;
        _yVelocity += _gravity * gravityScale * deltaTime;
    }

    private void StopAtCeiling(bool hitCeiling)
    {
        if (hitCeiling && _yVelocity > 0f)
        {
            _yVelocity = 0f;
        }
    }

    private void TryJump()
    {
        bool hasBufferedJump = _jumpBufferTimer > 0f;

        if (!hasBufferedJump || _jumpsRemaining <= 0)
        {
            return;
        }

        _yVelocity = _jumpPower;
        _jumpsRemaining--;
        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
    }

    private void CutJumpShort(bool jumpReleased)
    {
        if (jumpReleased && _yVelocity > 0f)
        {
            _yVelocity *= _jumpCutMultiplier;
        }
    }

    private void ClampFallSpeed()
    {
        _yVelocity = Mathf.Max(_yVelocity, -_maxFallSpeed);
    }
}
