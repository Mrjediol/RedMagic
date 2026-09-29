using DG.Tweening;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// TEMPORARY TEST — procedural animation of a floating enemy sprite using DOTween.
/// Setup: an empty parent GameObject holds this script; a child "Visual" holds the SpriteRenderer.
/// In Play Mode press 1 = hit, 2 = shoot, 3 = death, 4 = revive (or use the ContextMenu entries).
/// Delete this file when done testing.
public class HoverAnimTest : MonoBehaviour
{
    [Header("Refs (auto: first child)")]
    public Transform visual;

    [Header("Hover bob")]
    public float bobHeight = 0.12f;
    public float bobTime = 0.8f;

    [Header("Breathing squash")]
    public Vector2 breathScale = new Vector2(1.04f, 0.96f);
    public float breathTime = 0.8f;

    [Header("Tilt sway")]
    public float tiltDegrees = 4f;
    public float tiltTime = 1.3f;

    SpriteRenderer sr;
    Vector3 baseLocalPos, baseLocalScale;
    Color baseColor;
    Tween bobTween, breathTween, tiltTween;
    Sequence action;

    void Awake()
    {
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        sr = visual.GetComponentInChildren<SpriteRenderer>();
        baseLocalPos = visual.localPosition;
        baseLocalScale = visual.localScale;
        baseColor = sr.color;
    }


    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) TestHit();
        if (kb.digit2Key.wasPressedThisFrame) TestShoot();
        if (kb.digit3Key.wasPressedThisFrame) TestDeath();
        if (kb.digit4Key.wasPressedThisFrame) Revive();
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) TestHit();
        if (Input.GetKeyDown(KeyCode.Alpha2)) TestShoot();
        if (Input.GetKeyDown(KeyCode.Alpha3)) TestDeath();
        if (Input.GetKeyDown(KeyCode.Alpha4)) Revive();
#endif
    }

    void OnEnable() { StartIdle(); }
    void OnDisable() { KillIdle(); KillAction(); transform.DOKill(); if (sr != null) sr.DOKill(); }

    void KillIdle()
    {
        if (bobTween != null) bobTween.Kill();
        if (breathTween != null) breathTween.Kill();
        if (tiltTween != null) tiltTween.Kill();
    }

    void KillAction()
    {
        if (action != null) action.Kill();
    }

    void StartIdle()
    {
        KillIdle();
        visual.localPosition = baseLocalPos;
        visual.localScale = baseLocalScale;
        visual.localRotation = Quaternion.Euler(0f, 0f, -tiltDegrees);

        bobTween = visual.DOLocalMoveY(baseLocalPos.y + bobHeight, bobTime)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

        breathTween = visual.DOScale(new Vector3(baseLocalScale.x * breathScale.x,
                baseLocalScale.y * breathScale.y, baseLocalScale.z), breathTime)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

        tiltTween = visual.DOLocalRotate(new Vector3(0f, 0f, tiltDegrees), tiltTime)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    [ContextMenu("Test Hit")]
    public void TestHit()
    {
        // punch on the parent so it does not fight the idle tweens on the child
        transform.DOKill(true);
        transform.localScale = Vector3.one;
        transform.DOPunchScale(new Vector3(0.25f, -0.2f, 0f), 0.3f, 8, 0.6f);

        sr.DOKill(true);
        sr.color = baseColor;
        sr.DOColor(Color.red, 0.06f).SetLoops(2, LoopType.Yoyo);
    }

    [ContextMenu("Test Shoot")]
    public void TestShoot()
    {
        KillIdle();
        KillAction();
        Vector3 squashed = new Vector3(baseLocalScale.x * 0.93f, baseLocalScale.y * 1.07f, baseLocalScale.z);
        Vector3 stretched = new Vector3(baseLocalScale.x * 1.10f, baseLocalScale.y * 0.92f, baseLocalScale.z);

        action = DOTween.Sequence();
        // anticipation: lean back, compress
        action.Append(visual.DOLocalMoveX(baseLocalPos.x - 0.12f, 0.25f).SetEase(Ease.InOutQuad));
        action.Join(visual.DOScale(squashed, 0.25f));
        // fire: quick forward snap
        action.Append(visual.DOLocalMoveX(baseLocalPos.x + 0.10f, 0.05f).SetEase(Ease.OutExpo));
        action.Join(visual.DOScale(stretched, 0.05f));
        // recoil back to rest
        action.Append(visual.DOLocalMoveX(baseLocalPos.x, 0.35f).SetEase(Ease.OutBack));
        action.Join(visual.DOScale(baseLocalScale, 0.35f));
        action.OnComplete(StartIdle);
    }

    [ContextMenu("Test Death")]
    public void TestDeath()
    {
        KillIdle();
        KillAction();
        action = DOTween.Sequence();
        action.Append(visual.DOLocalRotate(new Vector3(0f, 0f, -25f), 0.15f));
        action.Append(visual.DOLocalMoveY(baseLocalPos.y - 0.6f, 0.5f).SetEase(Ease.InQuad));
        action.Join(visual.DOScale(baseLocalScale * 0.6f, 0.5f));
        action.Join(sr.DOFade(0f, 0.5f));
    }

    [ContextMenu("Revive")]
    public void Revive()
    {
        KillAction();
        sr.DOKill();
        sr.color = baseColor;
        StartIdle();
    }
}
