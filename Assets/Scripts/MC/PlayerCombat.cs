using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private CombatEventsBridge eventsBridge;
    [SerializeField] private CharacterController cc;        // opzionale se usi Transform driver
    [SerializeField] private PlayerController playerMove;   // locomotion esterna (per lock rotazione)
    [SerializeField] private Hitbox hitbox;
    [SerializeField] private CameraJolt cameraJolt;
    [SerializeField] private Hitstopper hitstopper;
    [SerializeField] private PlayerInput input;

    [Header("Animator params / layer")]
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string nextComboParam = "NextCombo";
    [SerializeField] private string isAttackingParam = "IsAttacking";

    [Header("Animator Layer")]
    [SerializeField] private string attackLayerName = "Attack_UpperBody";
    [SerializeField] private int attackLayerIndex = 1;

    [SerializeField] private float layerLerpUp = 18f;
    [SerializeField] private float layerLerpDown = 10f;

    [Header("Stati (Layer Attack)")]
    [SerializeField] private string attack1StateName = "Light_A1";
    [SerializeField] private string attack2StateName = "Light_A2";
    [SerializeField] private string attackLayerIdleState = ""; // opzionale

    // ====== Stato/combo ======
    private float nextAttackAllowedAt = 0f;
    private bool isAttacking;
    private int comboStep = 0; // 0=none, 1=A1, 2=A2
    private bool comboWindowOpen = false;
    private bool comboQueuedA2 = false;

    [Header("Early-Restart (solo fine A2)")]
    [SerializeField] private float a2RestartThresholdNormalized = 0.96f;

    [Header("Micro-grace per A1 (NO buffer/hold globale)")]
    [SerializeField] private float comboPreOpenGraceA1 = 0.08f;
    private bool preComboPressedA1 = false;
    private float preComboPressedAt = -999f;

    // ====== Micro-lunge fallback (in-place) ======
    [Header("Micro-lunge fallback")]
    [SerializeField] private float lungeDistance = 1.1f;
    [SerializeField] private float lungeDuration = 0.085f;

    [Header("Hit feedback")]
    [SerializeField] private float hitstopSeconds = 0.05f;
    [SerializeField] private float cameraJoltSeconds = 0.02f;

    // ====== G) Target-Assist & Gap-Closer ======
    [Header("G) Target-Assist — Parametri")]
    [SerializeField] private float assistAngleLight = 70f; // ±35°
    [SerializeField] private float assistRange = 5.0f;     // esteso a 5 m come dai log
    [SerializeField] private float assistWindow = 0.20f;   // allarga per test; poi 0.10
    [SerializeField] private float idealHitDistance = 0.8f;
    [SerializeField] private float maxLungeLight = 1.2f;
    [SerializeField] private float minAdvanceVisual = 0.12f; // “colpetto” minimo percepibile
    [SerializeField] private float turnSpeedDegPerSec = 900f;
    [SerializeField] private float heightTolerance = 1.5f;

    [Header("Ricerca target")]
    [SerializeField] private bool useTagFilter = false;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private LayerMask enemyMask = ~0;

    [Header("Sticky Target")]
    [SerializeField] private float stickyTimeout = 0.8f;

    [Header("LoS")]
    [SerializeField] private LayerMask losMask = ~0;

    // ====== Driver Movimento per Advance ======
    public enum AdvanceDriver { CharacterController, TransformTranslate }
    [Header("Advance Driver")]
    [SerializeField] private AdvanceDriver advanceDriver = AdvanceDriver.CharacterController;
    [SerializeField, Tooltip("Se true, switcha automaticamente a Translate se il CC è assente/disabilitato o lo spostamento resta ~0 per N frame.")]
    private bool autoFallbackToTranslate = true;
    [SerializeField, Tooltip("Quanti frame senza avanzare prima di fare fallback.")]
    private int stallFramesBeforeFallback = 3;

    // Stato assist
    private Transform stickyTarget;
    private float stickyUntil = -999f;
    private float attackStartTime = -999f;
    private bool assistLockedThisAttack = false;
    private Transform assistTarget;    // target scelto per l'attacco corrente
    private bool assistActivePhase = false; // true tra HitStart e HitEnd
    private Coroutine assistCR;        // coroutine advance assistito

    // ====== Debug ======
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool assistDebugLogs = false;

    // Runtime animator hashes / lerp
    private Coroutine layerCR, blendOutCR;
    private int attackTagHash, a1ShortHash, a2ShortHash;
    private bool hasIsAttackingParam, hasNextComboParam, hasAttackTrigger;

    // Input
    private const string MAP = "Gameplay";
    private const string ATTACK = "Attack";
    private InputAction attackAction;

    // NextCombo watchdog
    private float nextComboSetAt = -999f;
    private const float nextComboMaxHang = 0.6f;

    // Masks calcolate
    private LayerMask losMaskNoSelf;

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!eventsBridge) eventsBridge = animator ? animator.GetComponent<CombatEventsBridge>() : null;
        if (!cc) cc = GetComponent<CharacterController>();
        if (!cc) cc = GetComponentInChildren<CharacterController>(true);
        if (!playerMove) playerMove = GetComponent<PlayerController>();
        if (!playerMove) playerMove = GetComponentInChildren<PlayerController>(true);
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

        // LoS mask: escludi il layer del Player
        losMaskNoSelf = losMask;
        int myLayer = gameObject.layer;
        losMaskNoSelf &= ~(1 << myLayer);
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
        if (attackAction != null)
            attackAction.performed -= OnAttackPressed;

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

        // Autodetect ingresso A2
        if (st.shortNameHash == a2ShortHash && comboStep != 2)
        {
            comboStep = 2;
            if (hasNextComboParam) animator.SetBool(nextComboParam, false);
            nextComboSetAt = -999f;
            SetAttacking(true);
            StopBlendOut();
            SetAttackLayerWeightInstant(1f);
            if (debugLogs) Debug.Log("[Combat] Enter A2 → comboStep=2");
        }

        if (inAttackTag && w < 0.95f)
            SetAttackLayerWeightInstant(1f);

        // FINE A1
        if (comboStep == 1 &&
            st.shortNameHash == a1ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            if (comboQueuedA2 || (hasNextComboParam && animator.GetBool(nextComboParam)))
                ForceA2();
            else
                EndChainSmooth(false);
        }

        // FINE A2
        if (comboStep == 2 &&
            st.shortNameHash == a2ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            EndChainSmooth(true);
        }

        // Watchdog NextCombo
        if (hasNextComboParam && animator.GetBool(nextComboParam))
        {
            if (nextComboSetAt < 0f) nextComboSetAt = Time.time;
            bool inComboStates = (st.shortNameHash == a1ShortHash) || (st.shortNameHash == a2ShortHash);
            if (!inComboStates && !animator.IsInTransition(attackLayerIndex))
                HardResetFlags();
            else if (comboStep == 1 && Time.time - nextComboSetAt > nextComboMaxHang)
                ForceA2();
        }
        else nextComboSetAt = -999f;

        if (!isAttacking && w > 0.01f) SetAttackLayerWeight(0f);

        if (isAttacking && !inAttackTag && w <= 0.01f)
            ForceClearAttacking();
    }

    // ===================== INPUT =====================
    private void OnAttackPressed(InputAction.CallbackContext ctx)
    {
        if (!comboWindowOpen && IsAtEndOfA2()) { TryStartAttack1(); return; }

        if (!isAttacking) { TryStartAttack1(); return; }

        if (isAttacking && comboStep == 1)
        {
            if (comboWindowOpen)
            {
                comboQueuedA2 = true;
                if (hasNextComboParam) animator.SetBool(nextComboParam, true);
                nextComboSetAt = Time.time;
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

        comboStep = 1;
        comboQueuedA2 = false; comboWindowOpen = false;
        preComboPressedA1 = false;
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        nextComboSetAt = -999f;

        // finestra assist
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
                stickyUntil = Time.time + stickyTimeout;

                // --- NRE guard: verifica cc/animator prima di partire ---
                if (!animator)
                {
                    Debug.LogError("[Assist] Animator mancante su PlayerCombat.");
                    return;
                }
                if (!cc && advanceDriver == AdvanceDriver.CharacterController)
                {
                    // auto-fallback se CC mancante
                    advanceDriver = AdvanceDriver.TransformTranslate;
                    Debug.LogWarning("[Assist] CharacterController non assegnato/trovato → fallback a TransformTranslate.");
                }

                StartAssistAdvance(); // ← sicuro ora
            }
            else
            {
                if (assistDebugLogs) Debug.LogWarning("[Assist] Nessun target valido trovato.");
            }
        }

        StopBlendOut(); StopLayerLerp();
        SetAttackLayerWeightInstant(1f); SetAttacking(true);

        if (playerMove) playerMove.SetExternalSpeedMultiplier(0.55f);
        StartCoroutine(AimAssistLock(0.10f));
    }


    private void OnHitStart()
    {
        assistActivePhase = true;
        if (playerMove) playerMove.SetExternalSpeedMultiplier(0.80f);

        if (assistTarget == null)
        {
            if (lungeDistance > 0f && lungeDuration > 0f)
                StartCoroutine(DoLunge(lungeDistance, lungeDuration));
        }

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
        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

        bool comboIncoming = (comboStep == 1) && (comboQueuedA2 || (hasNextComboParam && animator.GetBool(nextComboParam)));
        if (!comboIncoming)
        {
            StartBlendOut(0.18f, 0.03f);
            if (comboStep == 1 || comboStep == 2) EndChain(false);
        }
        else
        {
            StopBlendOut();
            SetAttackLayerWeightInstant(1f);
        }
    }

    private void OnComboOpen()
    {
        comboWindowOpen = true;

        if (comboStep == 1 && preComboPressedA1 && (Time.time - preComboPressedAt) <= comboPreOpenGraceA1)
        {
            comboQueuedA2 = true;
            if (hasNextComboParam) animator.SetBool(nextComboParam, true);
            nextComboSetAt = Time.time;
        }
        preComboPressedA1 = false;
    }

    private void OnComboClose() { comboWindowOpen = false; }

    public void AttackClipEnd() { }

    // ===================== COMBO =====================
    private void ForceA2()
    {
        comboQueuedA2 = false;
        StopBlendOut(); StopLayerLerp();
        SetAttackLayerWeightInstant(1f);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        nextComboSetAt = -999f;

        comboStep = 2; SetAttacking(true);

        attackStartTime = Time.time; assistLockedThisAttack = false;
        assistTarget = null; assistActivePhase = false;
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
        nextComboSetAt = -999f;

        nextAttackAllowedAt = Time.time;
        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

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
        nextComboSetAt = -999f;

        nextAttackAllowedAt = Time.time;
        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

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

    private void HardResetFlags()
    {
        StopBlendOut(); SetAttackLayerWeightInstant(0f);
        comboStep = 0; comboQueuedA2 = false; comboWindowOpen = false;
        nextComboSetAt = -999f; ForceClearAttacking();
        nextAttackAllowedAt = Time.time; StopAssist();
    }

    // ===================== ASSIST: Acquire / Advance =====================
    private Transform AcquireAssistTarget(bool isHeavy)
    {
        LayerMask mask;
        if (enemyMask.value == 0)
            mask = ~0; // implicit conversion int -> LayerMask è supportata da Unity
        else
            mask = enemyMask;

        if (stickyTarget && Time.time <= stickyUntil && IsCandidateValid(stickyTarget, mask))
            return stickyTarget;

        float halfCone = Mathf.Max(1f, assistAngleLight * 0.5f);
        Collider[] cols = Physics.OverlapSphere(transform.position, assistRange, mask, QueryTriggerInteraction.Ignore);
        if (cols == null || cols.Length == 0) return null;

        Transform best = null; float bestScore = -1f;
        Vector3 origin = GetRayOrigin(); Vector3 fwd = transform.forward;

        foreach (var c in cols)
        {
            if (!c) continue;
            if (useTagFilter && !string.IsNullOrEmpty(enemyTag) && !c.CompareTag(enemyTag)) continue;

            Vector3 center = c.bounds.center;
            Vector3 to = center - transform.position;
            Vector3 toFlat = to; toFlat.y = 0f;

            float dist = toFlat.magnitude;
            if (dist < 0.5f || dist > assistRange) continue;

            float ourChestY = GetRayOrigin().y;
            float targetAtChestY = Mathf.Clamp(ourChestY, c.bounds.min.y, c.bounds.max.y);
            float dy = Mathf.Abs(targetAtChestY - ourChestY);
            if (dy > heightTolerance) continue;

            float angle = Vector3.Angle(fwd, toFlat);
            if (angle > halfCone) continue;

            Transform root = c.transform.root;
            Vector3 toCenter = center - origin;
            if (Physics.Raycast(origin, toCenter.normalized, out RaycastHit hit, toCenter.magnitude, losMaskNoSelf, QueryTriggerInteraction.Ignore))
                if (hit.transform.root != root) continue;

            float distTerm = 1f - (dist / assistRange);
            float angleTerm = Mathf.Cos(angle * Mathf.Deg2Rad);
            float stickyTerm = (stickyTarget && root == stickyTarget.root && Time.time <= stickyUntil) ? 0.1f : 0f;
            float score = 0.6f * distTerm + 0.3f * angleTerm + stickyTerm;

            if (assistDebugLogs) Debug.Log($"[Assist] OK '{c.name}': dist {dist:0.00}, ang {angle:0.0}°, dy {dy:0.00}, score {score:0.00}");

            if (score >= 0.4f && score > bestScore) { bestScore = score; best = root; }
        }

        if (assistDebugLogs && best) Debug.Log($"[Assist] Selezionato: '{best.name}' con score {bestScore:0.00}");
        return best;
    }

    private bool IsCandidateValid(Transform t, LayerMask maskForCheck)
    {
        if (!t) return false;

        Collider col = t.GetComponentInChildren<Collider>();
        if (!col) return false;

        Vector3 center = col.bounds.center;
        Vector3 to = center - transform.position;
        Vector3 toFlat = to; toFlat.y = 0f;

        float dist = toFlat.magnitude;
        if (dist < 0.5f || dist > assistRange) return false;

        float ourChestY = GetRayOrigin().y;
        float targetAtChestY = Mathf.Clamp(ourChestY, col.bounds.min.y, col.bounds.max.y);
        float dy = Mathf.Abs(targetAtChestY - ourChestY);
        if (dy > heightTolerance) return false;

        float halfCone = Mathf.Max(1f, assistAngleLight * 0.5f);
        float angle = Vector3.Angle(transform.forward, toFlat);
        if (angle > halfCone) return false;

        Vector3 origin = GetRayOrigin();
        Vector3 toCenter = center - origin;
        if (Physics.Raycast(origin, toCenter.normalized, out RaycastHit hit, toCenter.magnitude, losMaskNoSelf, QueryTriggerInteraction.Ignore))
            if (hit.transform.root != t.root) return false;

        if (useTagFilter && !string.IsNullOrEmpty(enemyTag) && !t.CompareTag(enemyTag)) return false;

        return true;
    }

    private void StartAssistAdvance()
    {
        StopAssist();
        assistCR = StartCoroutine(AssistAdvanceRoutine());
    }

    private void StopAssist()
    {
        if (assistCR != null) { StopCoroutine(assistCR); assistCR = null; }
        assistTarget = null; assistActivePhase = false;
    }

    private IEnumerator AssistAdvanceRoutine()
    {
        //if (!assistTarget) yield break;

        float advanceMax = maxLungeLight;
        float remaining = ComputeAdvanceDistance(assistTarget, idealHitDistance, advanceMax);

        // boost percettivo min
        if (remaining > 0f && remaining < minAdvanceVisual) remaining = minAdvanceVisual;

        if (assistDebugLogs) Debug.Log($"[Assist] Advance iniziale: {remaining:0.000} m (cap {advanceMax:0.00})");

        if (remaining <= 0.001f) yield break;

        float eased = 0f;
        float speed = remaining / 0.18f;

        // verifica CC presente/abilitato
        bool ccUsable = (advanceDriver == AdvanceDriver.CharacterController) && cc != null && cc.enabled;
        AdvanceDriver currentDriver = ccUsable ? AdvanceDriver.CharacterController : AdvanceDriver.TransformTranslate;

        int stallFrames = 0;

        while (assistTarget && (isAttacking || assistActivePhase))
        {
            // Rotazione verso il target
            Vector3 to = (assistTarget.position - transform.position); to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(to.normalized, Vector3.up);
                float stepDeg = turnSpeedDegPerSec * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, stepDeg);
            }

            if (remaining > 0f)
            {
                float dt = Time.deltaTime;
                float rawStep = speed * dt;
                float tNorm = Mathf.Clamp01(eased / Mathf.Max(remaining, 0.0001f));
                float ease = EaseInOutCubic(1f - tNorm);
                float step = Mathf.Min(remaining, rawStep * Mathf.Lerp(0.6f, 1.2f, ease));

                Vector3 delta = transform.forward * step;

                Vector3 before = transform.position;

                if (currentDriver == AdvanceDriver.CharacterController)
                {
                    // CC: prova move con taglio/slide
                    if (Physics.Raycast(GetRayOrigin(), delta.normalized, out RaycastHit hit, step + 0.05f, losMaskNoSelf, QueryTriggerInteraction.Ignore))
                    {
                        float allowed = Mathf.Max(0f, hit.distance - 0.02f);
                        Vector3 tryMove = delta.normalized * allowed;
                        Vector3 slide = Vector3.ProjectOnPlane(delta - tryMove, hit.normal);
                        cc.Move(tryMove + slide * 0.25f);
                    }
                    else
                    {
                        cc.Move(delta);
                    }
                }
                else
                {
                    // Translate: controllo anti-clipping basilare
                    Vector3 newPos = before + delta;
                    if (!Physics.CheckCapsule(before + Vector3.up * 0.5f, newPos + Vector3.up * 0.5f, 0.25f, losMaskNoSelf, QueryTriggerInteraction.Ignore))
                        transform.position = newPos;
                }

                float moved = (transform.position - before).magnitude;
                if (moved < 0.001f) stallFrames++; else stallFrames = 0;

                // auto fallback se stall
                if (autoFallbackToTranslate && currentDriver == AdvanceDriver.CharacterController && stallFrames >= stallFramesBeforeFallback)
                {
                    currentDriver = AdvanceDriver.TransformTranslate;
                    if (assistDebugLogs) Debug.LogWarning("[Assist] CC non avanza (stall) → fallback a TransformTranslate per questo attacco.");
                }

                remaining -= step;
                eased += step;
            }

            if (remaining <= 0.0001f) break;
            yield return null;
        }
        assistCR = null;
    }

    private float ComputeAdvanceDistance(Transform target, float ideal, float maxAdvance)
    {
        if (!target) return 0f;
        Collider col = target.GetComponentInChildren<Collider>();
        Vector3 targetPos = col ? col.bounds.center : target.position;

        Vector3 to = targetPos - transform.position; to.y = 0f;
        float dist = to.magnitude;
        float advance = Mathf.Clamp(dist - ideal, 0f, maxAdvance);

        if (dist < 0.4f) advance = Mathf.Min(advance, 0.3f);
        return advance;
    }

    private Vector3 GetRayOrigin() { Vector3 o = transform.position; o.y += 1.2f; return o; }
    private static float EaseInOutCubic(float x) => (x < 0.5f) ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

    // ===================== Lunge fallback / Aim lock =====================
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

    private IEnumerator AimAssistLock(float duration)
    {
        if (playerMove) playerMove.SetRotationOverride(true);
        float t = 0f; while (t < duration) { t += Time.deltaTime; yield return null; }
        if (playerMove) playerMove.SetRotationOverride(false);
    }

    // ===================== Layer weight & Blend =====================
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

    private bool IsAtEndOfA2()
    {
        var st = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        if (comboStep != 2) return false;
        if (st.shortNameHash != a2ShortHash) return false;
        if (animator.IsInTransition(attackLayerIndex)) return false;
        return st.normalizedTime >= a2RestartThresholdNormalized;
    }

    // ===================== Utils =====================
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
