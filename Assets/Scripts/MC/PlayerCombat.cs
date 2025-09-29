using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private CombatEventsBridge eventsBridge;
    [SerializeField] private CharacterController cc;        // opzionale
    [SerializeField] private PlayerController playerMove;   // per ApplyExternalFacing()
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
    private int comboStep = 0;              // 0=none,1=A1,2=A2
    private bool comboWindowOpen = false;
    private bool comboQueuedA2 = false;

    [Header("Fine A2 (early restart)")]
    [SerializeField] private float a2RestartThresholdNormalized = 0.96f;

    [Header("Micro-grace per A1")]
    [SerializeField] private float comboPreOpenGraceA1 = 0.08f;
    private bool preComboPressedA1 = false;
    private float preComboPressedAt = -999f;

    // ===== Stato facing per attacco corrente =====
    [Header("Refacing guard")]
    [Tooltip("Se true, durante questo attacco è consentito ruotare verso il target.")]
    private bool allowFacingThisAttack = false;
    [Tooltip("Diventa true solo se abbiamo davvero iniziato il facing (StartAttackFacing).")]
    private bool facingActiveThisAttack = false;
    private Transform facingTargetThisAttack = null;

    [Tooltip("Soglia di input (stick) oltre la quale si cancella il facing dell'attacco.")]
    [SerializeField] private float moveCancelFacingThreshold = 0.18f;
    [Tooltip("Dopo aver cancellato il facing, per questo tempo non permettiamo di rifarlo.")]
    [SerializeField] private float refacingCooldownSeconds = 0.30f;
    private float refacingCooldownUntil = -999f;

    // ===== Camera shake smoothing =====
    [Header("Camera shake (comfort)")]
    [Tooltip("Se la velocità planare supera questa soglia, riduciamo o annulliamo il jolt.")]
    [SerializeField] private float movingSpeedForNoJolt = 0.15f;
    [Tooltip("Fattore con cui ridurre il jolt quando stai muovendo (0 = disabilita, 1 = normale).")]
    [Range(0f, 1f)][SerializeField] private float joltWhileMovingScale = 0.0f; // di default: niente jolt mentre ti muovi
    [Tooltip("Se >0, usa questa durata più breve per il jolt quando ti muovi (se joltWhileMovingScale > 0).")]
    [SerializeField] private float cameraJoltSecondsWhileMoving = 0.008f;

    // ===== Debug =====
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Coroutine layerCR, blendOutCR;
    private int attackTagHash, a1ShortHash, a2ShortHash;
    private bool hasIsAttackingParam, hasNextComboParam, hasAttackTrigger;

    // Input
    private const string MAP = "Gameplay";
    private const string ATTACK = "Attack";
    private const string MOVE = "Move";
    private InputAction attackAction;
    private InputAction moveAction;

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!eventsBridge && animator) eventsBridge = animator.GetComponent<CombatEventsBridge>();
        if (!cc) cc = GetComponent<CharacterController>();
        if (!playerMove) playerMove = GetComponent<PlayerController>();
        if (!input) input = GetComponent<PlayerInput>();

        if (!string.IsNullOrEmpty(attackLayerName))
        {
            int idx = FindLayerIndexByName(animator, attackLayerName);
            if (idx >= 0) attackLayerIndex = idx;
        }
        attackLayerIndex = Mathf.Clamp(attackLayerIndex, 0, animator.layerCount - 1);

        attackTagHash = Animator.StringToHash("Attack");
        a1ShortHash = Animator.StringToHash(attack1StateName);
        a2ShortHash = Animator.StringToHash(attack2StateName);

        hasIsAttackingParam = HasAnimatorBool(animator, isAttackingParam);
        hasNextComboParam = HasAnimatorBool(animator, nextComboParam);
        hasAttackTrigger = HasAnimatorTrigger(animator, attackTrigger);
    }

    private void OnEnable()
    {
        var map = input.actions.FindActionMap(MAP, true);
        attackAction = map.FindAction(ATTACK, true);
        moveAction = map.FindAction(MOVE, false);

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
        // --- CANCELLAZIONE IMMEDIATA DEL FACING SU INPUT ---
        if (moveAction != null)
        {
            Vector2 mv = Vector2.zero;
            try { mv = moveAction.ReadValue<Vector2>(); } catch { }
            if (mv.sqrMagnitude >= moveCancelFacingThreshold * moveCancelFacingThreshold)
            {
                // se stai dando input e l’attacco stava forzando facing → stop + cooldown
                if (facingActiveThisAttack || allowFacingThisAttack)
                    CancelFacingForThisAttack(startCooldown: true);
            }
        }

        var st = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        float w = animator.GetLayerWeight(attackLayerIndex);
        bool inAttackTag = (st.tagHash == attackTagHash);

        if (st.shortNameHash == a2ShortHash && comboStep != 2)
        {
            comboStep = 2;
            if (hasNextComboParam) animator.SetBool(nextComboParam, false);
            SetAttacking(true);
            StopBlendOut();
            SetAttackLayerWeightInstant(1f);
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

        // reset facing state per questo attacco
        allowFacingThisAttack = false;
        facingActiveThisAttack = false;
        facingTargetThisAttack = null;

        StopBlendOut(); StopLayerLerp();
        SetAttackLayerWeightInstant(1f);
        SetAttacking(true);

        comboStep = 1;
        comboQueuedA2 = false;
        comboWindowOpen = false;
        preComboPressedA1 = false;
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);

        if (!string.IsNullOrEmpty(attack1StateName))
            animator.CrossFadeInFixedTime(attack1StateName, 0.05f, attackLayerIndex, 0f);
        else if (hasAttackTrigger) { animator.ResetTrigger(attackTrigger); animator.SetTrigger(attackTrigger); }
    }

    // ===================== EVENTI CLIP =====================
    private void OnWindupStart()
    {
        if (assistTargeting != null)
        {
            // se siamo in cooldown anti-refacing → non concedere facing
            if (Time.time >= refacingCooldownUntil)
            {
                var t = assistTargeting.AcquireAssistTarget();
                bool should = (t != null) && assistTargeting.ShouldFaceNow(t);
                allowFacingThisAttack = should;

                if (should)
                {
                    facingTargetThisAttack = t;
                    assistTargeting.StartAttackFacing(t);
                    facingActiveThisAttack = assistTargeting.IsFacingActive;

                    if (facingActiveThisAttack && playerMove != null)
                        playerMove.ApplyExternalFacing(assistTargeting.LastLockedRotation, 0.05f);

                    if (assistTargeting.IsEligibleForAssist(t))
                        StartCoroutine(assistTargeting.DoAssistCoroutine(t));
                }
            }
        }

        StopBlendOut(); StopLayerLerp();
        SetAttackLayerWeightInstant(1f); SetAttacking(true);
    }

    private void OnHitStart()
    {
        // failsafe: abilita facing solo se non in cooldown
        if (!allowFacingThisAttack && assistTargeting != null && Time.time >= refacingCooldownUntil)
        {
            var t = assistTargeting.AcquireAssistTarget();
            if (t != null && assistTargeting.ShouldFaceNow(t))
            {
                allowFacingThisAttack = true;
                facingTargetThisAttack = t;
                assistTargeting.StartAttackFacing(t);
                facingActiveThisAttack = assistTargeting.IsFacingActive;

                if (facingActiveThisAttack && playerMove != null)
                    playerMove.ApplyExternalFacing(assistTargeting.LastLockedRotation, 0.03f);

                if (assistTargeting.IsEligibleForAssist(t))
                    StartCoroutine(assistTargeting.DoAssistCoroutine(t));
            }
        }

        if (hitbox) { hitbox.BeginSwing(); hitbox.SetActive(true); }
    }

    private void OnHitEnd()
    {
        if (hitbox) hitbox.SetActive(false);

        // ---- Camera jolt comfort ----
        if (cameraJolt)
        {
            float speed = 0f;
            if (playerMove != null) speed = playerMove.GetPlanarVelocity().magnitude;

            if (speed <= movingSpeedForNoJolt)
            {
                // fermo → jolt pieno
                cameraJolt.DoJolt(0.1f);
            }
            else if (joltWhileMovingScale > 0f)
            {
                // in movimento → jolt ridotto
                float dur = (cameraJoltSecondsWhileMoving > 0f) ? cameraJoltSecondsWhileMoving : 0.1f * joltWhileMovingScale;
                cameraJolt.DoJolt(dur);
            }
            // altrimenti (joltWhileMovingScale==0) niente jolt mentre cammini
        }

        if (hitstopper) hitstopper.DoHitstop(0.05f);
    }

    private void OnRecoverStart()
    {
        FinalizeFacingNow();   // solo se abbiamo davvero ruotato in questo attacco

        bool goingCombo = (comboStep == 1) && hasNextComboParam && animator.GetBool(nextComboParam);
        if (!goingCombo) EndChainSmooth(comboStep == 2);
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

    public void AttackClipEnd() { FinalizeFacingNow(); }

    // ======= Finale/cancellazione facing =======
    private void FinalizeFacingNow()
    {
        if (!allowFacingThisAttack || !facingActiveThisAttack || assistTargeting == null) return;

        assistTargeting.EndAttackFacing(applyFinalFacing: true);

        if (playerMove != null)
            playerMove.ApplyExternalFacing(assistTargeting.LastLockedRotation, 0.04f);

        assistTargeting.FullReleaseFacing();

        facingActiveThisAttack = false;
        allowFacingThisAttack = false;
        facingTargetThisAttack = null;
    }

    private void CancelFacingForThisAttack(bool startCooldown)
    {
        if (assistTargeting != null) assistTargeting.FullReleaseFacing();
        allowFacingThisAttack = false;
        facingActiveThisAttack = false;
        facingTargetThisAttack = null;

        if (startCooldown)
            refacingCooldownUntil = Time.time + refacingCooldownSeconds;

        if (debugLogs) Debug.Log($"[Combat] Facing cancellato. Cooldown fino a {refacingCooldownUntil:0.00}");
    }

    // ===================== COMBO / FINE =====================
    private void ForceA2()
    {
        comboQueuedA2 = false;
        StopBlendOut(); StopLayerLerp();
        SetAttackLayerWeightInstant(1f);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);

        comboStep = 2; SetAttacking(true);
        if (!string.IsNullOrEmpty(attack2StateName))
            animator.CrossFadeInFixedTime(attack2StateName, 0.05f, attackLayerIndex, 0f);
    }

    private void EndChainSmooth(bool fromA2)
    {
        comboStep = 0;
        comboQueuedA2 = false;
        comboWindowOpen = false;
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
    }

    private IEnumerator ForceUnlockNextFrame() { yield return null; ForceClearAttacking(); }

    private void ForceClearAttacking()
    {
        isAttacking = false;
        if (hasIsAttackingParam) animator.SetBool(isAttackingParam, false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);
    }

    // ===== Blend helpers =====
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
        StopLayerLerp();
        layerCR = StartCoroutine(LayerLerpRoutine(target));
    }
    private void StopLayerLerp()
    {
        if (layerCR != null) { StopCoroutine(layerCR); layerCR = null; }
    }
    private void SetAttackLayerWeightInstant(float target)
    {
        StopLayerLerp();
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

    // ===== Utils Animator =====
    private static bool HasAnimatorBool(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name) return true;
        return false;
    }
    private static bool HasAnimatorTrigger(Animator a, string name)
    {
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
    }
    private static int FindLayerIndexByName(Animator a, string layerName)
    {
        if (a == null || string.IsNullOrEmpty(layerName)) return -1;
        for (int i = 0; i < a.layerCount; i++)
            if (a.GetLayerName(i) == layerName) return i;
        return -1;
    }
}
