using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private CombatEventsBridge eventsBridge;
    [SerializeField] private CharacterController cc;
    [SerializeField] private PlayerController playerMove;   // se presente, NON useremo rotation override
    [SerializeField] private Hitbox hitbox;
    [SerializeField] private CameraJolt cameraJolt;
    [SerializeField] private Hitstopper hitstopper;
    [SerializeField] private PlayerInput input;
    [SerializeField] private AssistTargeting assistTargeting;

    [Header("Animator params / layer")]
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string nextComboParam = "NextCombo";
    [SerializeField] private string isAttackingParam = "IsAttacking";

    [Header("Animator Layer")]
    [SerializeField] private string attackLayerName = "Attack_UpperBody";
    [SerializeField] private int attackLayerIndex = 1;
    [SerializeField] private float layerLerpUp = 18f;
    [SerializeField] private float layerLerpDown = 10f;

    [Header("Stati (Attack Layer)")]
    [SerializeField] private string attack1StateName = "Light_A1";
    [SerializeField] private string attack2StateName = "Light_A2";
    [SerializeField] private string attackLayerIdleState = "";

    // ===== Stato / Combo =====
    private float nextAttackAllowedAt = 0f;
    private bool isAttacking;
    private int comboStep = 0;
    private bool comboWindowOpen = false;
    private bool comboQueuedA2 = false;

    [Header("Early-Restart (fine A2)")]
    [SerializeField] private float a2RestartThresholdNormalized = 0.96f;

    [Header("Micro-grace per A1")]
    [SerializeField] private float comboPreOpenGraceA1 = 0.08f;
    private bool preComboPressedA1 = false;
    private float preComboPressedAt = -999f;

    // ===== Micro-lunge fallback =====
    [Header("Micro-lunge fallback (solo se NO assist)")]
    [SerializeField] private float lungeDistance = 1.1f;
    [SerializeField] private float lungeDuration = 0.085f;

    [Header("Hit feedback")]
    [SerializeField] private float hitstopSeconds = 0.05f;
    [SerializeField] private float cameraJoltSeconds = 0.02f;

    // ===== Assist/gap close (solo quanto serve qui) =====
    [Header("Assist — Parametri")]
    [SerializeField] private float assistAngleLight = 70f;
    [SerializeField] private float assistRange = 5.0f;
    [SerializeField] private float assistWindow = 0.20f;
    [SerializeField] private float idealHitDistance = 0.8f;
    [SerializeField] private float maxLungeLight = 1.2f;
    [SerializeField] private float minAdvanceVisual = 0.12f;
    [SerializeField] private float turnSpeedDegPerSec = 900f;
    [SerializeField] private float heightTolerance = 1.5f;

    [Header("Ricerca target")]
    [SerializeField] private bool useTagFilter = false;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private LayerMask enemyMask = ~0;

    [Header("LoS")]
    [SerializeField] private LayerMask losMask = ~0;

    // ===== Driver advance (se usi lunge locale) =====
    public enum AdvanceDriver { CharacterController, TransformTranslate }
    [Header("Advance Driver")]
    [SerializeField] private AdvanceDriver advanceDriver = AdvanceDriver.CharacterController;

    // ===== Stato assist =====
    private Transform stickyTarget;
    private float stickyUntil = -999f;
    private float attackStartTime = -999f;
    private bool assistLockedThisAttack = false;
    private Transform assistTarget;
    private bool assistActivePhase = false;
    private Coroutine assistCR;

    // ===== Debug =====
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool assistDebugLogs = false;

    private Coroutine layerCR, blendOutCR, advParamCR;
    private int attackTagHash, a1ShortHash, a2ShortHash;
    private bool hasIsAttackingParam, hasNextComboParam, hasAttackTrigger;

    // Input
    private const string MAP = "Gameplay";
    private const string ATTACK = "Attack";
    private InputAction attackAction;

    private LayerMask losMaskNoSelf;

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!eventsBridge && animator) eventsBridge = animator.GetComponent<CombatEventsBridge>();
        if (!cc) cc = GetComponent<CharacterController>();
        if (!playerMove) playerMove = GetComponent<PlayerController>();
        if (!input) input = GetComponent<PlayerInput>();

        if (!string.IsNullOrEmpty(attackLayerName))
        {
            int found = FindLayerIndexByName(animator, attackLayerName);
            if (found >= 0) attackLayerIndex = found;
        }
        attackLayerIndex = Mathf.Clamp(attackLayerIndex, 0, animator.layerCount - 1);

        attackTagHash = Animator.StringToHash("Attack");
        a1ShortHash = Animator.StringToHash(attack1StateName);
        a2ShortHash = Animator.StringToHash(attack2StateName);

        hasIsAttackingParam = HasAnimatorBool(animator, isAttackingParam);
        hasNextComboParam = HasAnimatorBool(animator, nextComboParam);
        hasAttackTrigger = HasAnimatorTrigger(animator, attackTrigger);

        losMaskNoSelf = losMask & ~(1 << gameObject.layer);
    }

    private void OnEnable()
    {
        var map = input.actions.FindActionMap(MAP, true);
        attackAction = map.FindAction(ATTACK, true);
        attackAction.performed += OnAttackPressed;

        if (eventsBridge)
        {
            eventsBridge.OnWindupStart += OnWindupStart;
            eventsBridge.OnHitStart += OnHitStart;
            eventsBridge.OnHitEnd += OnHitEnd;
            eventsBridge.OnRecoverStart += OnRecoverStart;
            eventsBridge.OnComboOpen += OnComboOpen;
            eventsBridge.OnComboClose += OnComboClose;
        }

        animator.SetLayerWeight(attackLayerIndex, 0f);
    }

    private void OnDisable()
    {
        if (attackAction != null) attackAction.performed -= OnAttackPressed;
        if (eventsBridge)
        {
            eventsBridge.OnWindupStart -= OnWindupStart;
            eventsBridge.OnHitStart -= OnHitStart;
            eventsBridge.OnHitEnd -= OnHitEnd;
            eventsBridge.OnRecoverStart -= OnRecoverStart;
            eventsBridge.OnComboOpen -= OnComboOpen;
            eventsBridge.OnComboClose -= OnComboClose;
        }
    }

    private void Update()
    {
        var st = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        float w = animator.GetLayerWeight(attackLayerIndex);
        bool inAttackTag = (st.tagHash == attackTagHash);

        if (st.shortNameHash == a2ShortHash && comboStep != 2)
        {
            comboStep = 2;
            if (hasNextComboParam) animator.SetBool(nextComboParam, false);
            SetAttacking(true);
            StopBlendOut(); SetAttackLayerWeightInstant(1f);
        }

        if (inAttackTag && w < 0.95f) SetAttackLayerWeightInstant(1f);

        if (comboStep == 1 && st.shortNameHash == a1ShortHash && st.normalizedTime >= 0.98f && !animator.IsInTransition(attackLayerIndex))
        {
            if (comboQueuedA2 || (hasNextComboParam && animator.GetBool(nextComboParam))) ForceA2();
            else EndChainSmooth(false);
        }

        if (comboStep == 2 && st.shortNameHash == a2ShortHash && st.normalizedTime >= 0.98f && !animator.IsInTransition(attackLayerIndex))
            EndChainSmooth(true);

        if (!isAttacking && w > 0.01f) SetAttackLayerWeight(0f);
        if (isAttacking && !inAttackTag && w <= 0.01f) ForceClearAttacking();
    }

    // ===================== INPUT =====================
    private void OnAttackPressed(InputAction.CallbackContext ctx)
    {
        if (!isAttacking) { TryStartAttack1(); return; }
        if (isAttacking && comboStep == 1)
        {
            if (comboWindowOpen)
            {
                comboQueuedA2 = true;
                if (hasNextComboParam) animator.SetBool(nextComboParam, true);
            }
            else
            {
                preComboPressedA1 = true;
                preComboPressedAt = Time.time;
            }
        }
    }

    private bool CanStartAttack1() => Time.time >= nextAttackAllowedAt;

    private void TryStartAttack1()
    {
        if (!CanStartAttack1()) return;

        StopBlendOut(); StopLayerLerp();
        SetAttackLayerWeightInstant(1f); SetAttacking(true);

        comboStep = 1; comboQueuedA2 = false; comboWindowOpen = false;
        preComboPressedA1 = false;
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);

        attackStartTime = Time.time;
        assistLockedThisAttack = false;
        assistTarget = null;
        assistActivePhase = false;
        StopAssist();

        if (!string.IsNullOrEmpty(attack1StateName))
            animator.CrossFadeInFixedTime(attack1StateName, 0.05f, attackLayerIndex, 0f);
        else if (hasAttackTrigger) { animator.ResetTrigger(attackTrigger); animator.SetTrigger(attackTrigger); }
    }

    // ===================== EVENTI CLIP =====================
    private void OnWindupStart()
    {
        bool insideWindow = (Time.time - attackStartTime) <= assistWindow;

        if (!assistLockedThisAttack && insideWindow)
        {
            assistTarget = AcquireAssistTarget(false);
            if (assistTarget != null)
            {
                assistLockedThisAttack = true;
                stickyTarget = assistTarget;
                stickyUntil = Time.time + 0.8f;
                //StartAssistAdvance();
            }
        }

        // Facing + (opz) dash dal sistema AssistTargeting
        if (assistTargeting != null)
        {
            var t = assistTargeting.AcquireAssistTarget();
            if (t != null)
            {
                assistTargeting.StartAttackFacing(t);
                StartCoroutine(assistTargeting.DoAssistCoroutine(t));
            }
        }

        StopBlendOut(); StopLayerLerp();
        SetAttackLayerWeightInstant(1f); SetAttacking(true);
    }

    private void OnHitStart()
    {
        assistActivePhase = true;

        if (assistTarget == null && lungeDistance > 0f && lungeDuration > 0f)
            StartCoroutine(DoLunge(lungeDistance, lungeDuration));

        if (hitbox) { hitbox.BeginSwing(); hitbox.SetActive(true); }
    }

    private void OnHitEnd()
    {
        assistActivePhase = false;
        StopAssist();

        if (hitbox) hitbox.SetActive(false);
        if (hitstopper) hitstopper.DoHitstop(hitstopSeconds);
        if (cameraJolt) cameraJolt.DoJolt(cameraJoltSeconds);
    }

    private void OnRecoverStart()
    {
        bool comboIncoming = (comboStep == 1) && (comboQueuedA2 || (hasNextComboParam && animator.GetBool(nextComboParam)));
        if (!comboIncoming) { StartBlendOut(0.18f, 0.03f); EndChain(false); }
        else { StopBlendOut(); SetAttackLayerWeightInstant(1f); }
    }

    private void OnComboOpen()
    {
        comboWindowOpen = true;
        if (comboStep == 1 && preComboPressedA1 && (Time.time - preComboPressedAt) <= comboPreOpenGraceA1)
        {
            comboQueuedA2 = true;
            if (hasNextComboParam) animator.SetBool(nextComboParam, true);
        }
        preComboPressedA1 = false;
    }
    private void OnComboClose() { comboWindowOpen = false; }

    /// Animation Event al termine della clip d’attacco
    public void AttackClipEnd()
    {
        // 1) imponi UNA VOLTA la rotazione buona calcolata
        if (assistTargeting != null)
        {
            assistTargeting.EndAttackFacing(applyFinalFacing: true);
            transform.rotation = assistTargeting.LastLockedRotation;
            assistTargeting.FullReleaseFacing(); // KILL SWITCH: niente update dopo
        }
        // 2) nessun override, nessuna finestra post-hold → il controller torna libero SUBITO.
    }

    // ===================== COMBO =====================
    private void ForceA2()
    {
        comboQueuedA2 = false;
        StopBlendOut(); StopLayerLerp();
        SetAttackLayerWeightInstant(1f);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);

        comboStep = 2; SetAttacking(true);

        attackStartTime = Time.time;
        assistLockedThisAttack = false;
        assistTarget = null;
        assistActivePhase = false;
        StopAssist();

        animator.CrossFadeInFixedTime(attack2StateName, 0.05f, attackLayerIndex, 0f);
    }

    // ===================== FINE CATENA =====================
    private void EndChainSmooth(bool fromA2)
    {
        comboStep = 0; comboQueuedA2 = false; comboWindowOpen = false;
        SetAttacking(false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);

        nextAttackAllowedAt = Time.time;

        StartBlendOut(fromA2 ? 0.24f : 0.18f, 0.03f);

        if (!string.IsNullOrEmpty(attackLayerIdleState))
        {
            int idleHash = Animator.StringToHash(attackLayerIdleState);
            if (animator.HasState(attackLayerIndex, idleHash))
                animator.Play(attackLayerIdleState, attackLayerIndex, 0f);
        }

        StartCoroutine(ForceUnlockNextFrame());
        StopAssist();
    }

    private void EndChain(bool forceWeightZero = true)
    {
        comboStep = 0; comboQueuedA2 = false; comboWindowOpen = false;
        SetAttacking(false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);

        nextAttackAllowedAt = Time.time;

        if (forceWeightZero) SetAttackLayerWeightInstant(0f);

        if (!string.IsNullOrEmpty(attackLayerIdleState))
        {
            int idleHash = Animator.StringToHash(attackLayerIdleState);
            if (animator.HasState(attackLayerIndex, idleHash))
                animator.Play(attackLayerIdleState, attackLayerIndex, 0f);
        }

        StartCoroutine(ForceUnlockNextFrame());
        StopAssist();
    }

    private IEnumerator ForceUnlockNextFrame() { yield return null; ForceClearAttacking(); }

    private void ForceClearAttacking()
    {
        isAttacking = false;
        if (hasIsAttackingParam) animator.SetBool(isAttackingParam, false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);
    }

    private void StopAssist()
    {
        if (assistCR != null) { StopCoroutine(assistCR); assistCR = null; }
        assistTarget = null;
        assistActivePhase = false;
    }

    // ====== util ======
    private IEnumerator DoLunge(float distance, float duration)
    {
        if (distance <= 0f || duration <= 0f) yield break;
        float moved = 0f;
        while (moved < distance)
        {
            float step = (distance / duration) * Time.deltaTime;
            Vector3 delta = transform.forward * step;
            if (advanceDriver == AdvanceDriver.CharacterController && cc && cc.enabled) cc.Move(delta);
            else transform.position += delta;
            moved += step;
            yield return null;
        }
    }

    private void StartBlendOut(float duration, float delay)
    {
        StopBlendOut();
        blendOutCR = StartCoroutine(BlendOutRoutine(duration, delay));
    }
    private void StopBlendOut()
    {
        if (blendOutCR != null) { StopCoroutine(blendOutCR); blendOutCR = null; }
    }
    private IEnumerator BlendOutRoutine(float duration, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float start = animator.GetLayerWeight(attackLayerIndex);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float w = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / duration));
            animator.SetLayerWeight(attackLayerIndex, w);
            yield return null;
        }
        animator.SetLayerWeight(attackLayerIndex, 0f);
        blendOutCR = null;
    }
    private void SetAttackLayerWeight(float target)
    {
        if (layerCR != null) StopCoroutine(layerCR);
        layerCR = StartCoroutine(LayerLerpRoutine(target));
    }
    private void StopLayerLerp()
    {
        if (layerCR != null) { StopCoroutine(layerCR); layerCR = null; }
    }
    private void SetAttackLayerWeightInstant(float target)
    {
        if (layerCR != null) { StopCoroutine(layerCR); layerCR = null; }
        animator.SetLayerWeight(attackLayerIndex, target);
    }
    private IEnumerator LayerLerpRoutine(float target)
    {
        float w = animator.GetLayerWeight(attackLayerIndex);
        while (!Mathf.Approximately(w, target))
        {
            float speed = (target > w) ? layerLerpUp : layerLerpDown;
            w = Mathf.MoveTowards(w, target, speed * Time.deltaTime);
            animator.SetLayerWeight(attackLayerIndex, w);
            yield return null;
        }
        animator.SetLayerWeight(attackLayerIndex, target);
    }

    private void SetAttacking(bool v)
    {
        isAttacking = v;
        if (hasIsAttackingParam) animator.SetBool(isAttackingParam, v);
    }

    private static bool HasAnimatorBool(Animator a, string name)
    {
        foreach (var p in a.parameters) if (p.type == AnimatorControllerParameterType.Bool && p.name == name) return true;
        return false;
    }
    private static bool HasAnimatorTrigger(Animator a, string name)
    {
        foreach (var p in a.parameters) if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
    }
    private static int FindLayerIndexByName(Animator a, string layerName)
    {
        if (a == null || string.IsNullOrEmpty(layerName)) return -1;
        for (int i = 0; i < a.layerCount; i++) if (a.GetLayerName(i) == layerName) return i;
        return -1;
    }

    // ---- Stub minimi per AcquireAssistTarget / Check LOS (se li usi) ----
    private Transform AcquireAssistTarget(bool isHeavy) { return null; } // usi AssistTargeting
    private Vector3 GetRayOrigin() { var o = transform.position; o.y += 1.2f; return o; }
}
