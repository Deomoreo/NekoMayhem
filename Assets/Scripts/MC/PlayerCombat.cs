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
    [SerializeField] private CharacterController cc;
    [SerializeField] private PlayerController playerMove;
    [SerializeField] private Hitbox hitbox;
    [SerializeField] private CameraJolt cameraJolt;
    [SerializeField] private Hitstopper hitstopper;
    [SerializeField] private PlayerInput input;

    [Header("Animator params / layer")]
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string nextComboParam = "NextCombo";
    [SerializeField] private string isAttackingParam = "IsAttacking";

    [Header("Animator Layer")]
    [SerializeField, Tooltip("Nome del layer attacchi (Override). E.g. Attack_UpperBody")]
    private string attackLayerName = "Attack_UpperBody";
    [SerializeField, Tooltip("Indice fallback se il nome non combacia")]
    private int attackLayerIndex = 1;

    [SerializeField] private float layerLerpUp = 18f;
    [SerializeField] private float layerLerpDown = 10f;

    [Header("Stati (Layer Attack)")]
    [SerializeField] private string attack1StateName = "Light_A1";
    [SerializeField] private string attack2StateName = "Light_A2";
    [SerializeField] private string attackLayerIdleState = ""; // opzionale

    // ====== Stato/combo ======
    private float nextAttackAllowedAt = 0f; // no cooldown forzato
    private bool isAttacking;
    private int comboStep = 0; // 0=none, 1=A1, 2=A2
    private bool comboWindowOpen = false;
    private bool comboQueuedA2 = false;

    [Header("Early-Restart (solo fine A2)")]
    [SerializeField, Tooltip("Se A2 oltre questa normalized time e finestra NON aperta, click → A1.")]
    private float a2RestartThresholdNormalized = 0.96f;

    [Header("Micro-grace per A1 (NO buffer/hold globale)")]
    [SerializeField, Tooltip("Premi ≤X s PRIMA di ComboOpen (A1) → A2 accettata.")]
    private float comboPreOpenGraceA1 = 0.08f;
    private bool preComboPressedA1 = false;
    private float preComboPressedAt = -999f;

    // ====== Lunge/Lunge fallback (pre-esistente) ======
    [Header("Micro-lunge fallback (in-place)")]
    [SerializeField] private float lungeDistance = 1.1f;
    [SerializeField] private float lungeDuration = 0.085f;

    [Header("Hit feedback")]
    [SerializeField] private float hitstopSeconds = 0.05f;
    [SerializeField] private float cameraJoltSeconds = 0.02f;

    // ====== G) Target-Assist & Gap-Closer (Light/Heavy) ======
    [Header("G) Target-Assist — Parametri")]
    [SerializeField, Tooltip("Gradi totali del cono (Light) es. 60 ⇒ ±30°")]
    private float assistAngleLight = 60f; // cono 60° ⇒ ±30°
    [SerializeField, Tooltip("Gradi totali del cono (Heavy) es. 40 ⇒ ±20°")]
    private float assistAngleHeavy = 40f; // non usato qui, ma pronto
    [SerializeField] private float assistRange = 2.5f;
    [SerializeField, Tooltip("Finestra temporale dall'avvio colpo")]
    private float assistWindow = 0.10f;
    [SerializeField] private float idealHitDistance = 0.9f;
    [SerializeField] private float maxLungeLight = 1.1f; // cap Light
    [SerializeField] private float turnSpeedDegPerSec = 720f; // startup slerp
    [SerializeField] private float stickyTimeout = 0.8f;
    [SerializeField] private float heightTolerance = 0.5f;
    [SerializeField, Tooltip("LayerMask nemici per Overlap/Raycast")]
    private LayerMask enemyMask = ~0;
    [SerializeField] private string enemyTag = "Enemy";

    // Stato assist
    private Transform stickyTarget;
    private float stickyUntil = -999f;
    private float attackStartTime = -999f;
    private bool assistLockedThisAttack = false;
    private Transform assistTarget;   // target scelto per l'attacco corrente
    private bool assistActivePhase = false; // true tra HitStart e HitEnd
    private Coroutine assistCR;       // coroutine advance assistito

    // ====== Debug ======
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

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

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!eventsBridge) eventsBridge = animator ? animator.GetComponent<CombatEventsBridge>() : null;
        if (!cc) cc = GetComponent<CharacterController>();
        if (!playerMove) playerMove = GetComponent<PlayerController>();
        if (!input) input = GetComponent<PlayerInput>();

        if (!string.IsNullOrEmpty(attackLayerName))
        {
            int found = FindLayerIndexByName(animator, attackLayerName);
            if (found >= 0) attackLayerIndex = found;
            else if (debugLogs) Debug.LogWarning($"[Combat] Layer '{attackLayerName}' non trovato. Uso indice {attackLayerIndex}.");
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

        // Safety: in tag Attack tieni il layer alto SUBITO
        if (inAttackTag && w < 0.95f)
            SetAttackLayerWeightInstant(1f);

        // FINE A1 → A2 se queue; altrimenti end
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

        // FINE A2 → end
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
            {
                if (debugLogs) Debug.LogWarning("[Combat] NEXT stuck fuori da Attack → reset");
                HardResetFlags();
            }
            else if (comboStep == 1 && Time.time - nextComboSetAt > nextComboMaxHang)
            {
                if (debugLogs) Debug.LogWarning("[Combat] NEXT appeso troppo → FORCE A2");
                ForceA2();
            }
        }
        else nextComboSetAt = -999f;

        // Abbassa layer quando non attacco
        if (!isAttacking && w > 0.01f) SetAttackLayerWeight(0f);

        // failsafe
        if (isAttacking && !inAttackTag && w <= 0.01f)
            ForceClearAttacking();
    }

    // ===================== INPUT =====================
    private void OnAttackPressed(InputAction.CallbackContext ctx)
    {
        // Early-restart SOLO da fine A2 (finestra chiusa)
        if (!comboWindowOpen && IsAtEndOfA2())
        {
            TryStartAttack1();
            return;
        }

        // Primo click: avvia A1 se non stai attaccando
        if (!isAttacking)
        {
            TryStartAttack1();
            return;
        }

        // Sei in A1
        if (isAttacking && comboStep == 1)
        {
            if (comboWindowOpen)
            {
                comboQueuedA2 = true;
                if (hasNextComboParam) animator.SetBool(nextComboParam, true);
                nextComboSetAt = Time.time;
                if (debugLogs) Debug.Log("[Combat] Combo A2 QUEUED (press in A1 window)");
            }
            else
            {
                // Micro-grace A1
                preComboPressedA1 = true;
                preComboPressedAt = Time.time;
                if (debugLogs) Debug.Log("[Combat] Pre-Combo A2 micro-grace armed");
            }
        }
    }

    private bool CanStartAttack1() => Time.time >= nextAttackAllowedAt;

    private void TryStartAttack1()
    {
        if (!CanStartAttack1()) return;

        StopBlendOut();
        StopLayerLerp();
        SetAttackLayerWeightInstant(1f);
        SetAttacking(true);

        comboStep = 1;
        comboQueuedA2 = false;
        comboWindowOpen = false;
        preComboPressedA1 = false;
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        nextComboSetAt = -999f;

        // ====== G) inizio finestra assist ======
        attackStartTime = Time.time;
        assistLockedThisAttack = false;
        assistTarget = null;
        assistActivePhase = false;
        StopAssist(); // cancella eventuale residuo

        if (!string.IsNullOrEmpty(attack1StateName))
            animator.CrossFadeInFixedTime(attack1StateName, 0.05f, attackLayerIndex, 0f);
        else if (hasAttackTrigger)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }

        if (debugLogs) Debug.Log($"[Combat] Attack1 start (layerIdx={attackLayerIndex}, weight={animator.GetLayerWeight(attackLayerIndex):0.00})");
    }

    // ===================== EVENTI CLIP =====================
    private void OnWindupStart()
    {
        // Aggancio assist SOLO se siamo dentro la finestra (0.10s) e non già lockato
        if (!assistLockedThisAttack && (Time.time - attackStartTime) <= assistWindow)
        {
            assistTarget = AcquireAssistTarget(isHeavy: false);
            if (assistTarget != null)
            {
                assistLockedThisAttack = true;
                stickyTarget = assistTarget;
                stickyUntil = Time.time + stickyTimeout;
                StartAssistAdvance(); // muove/ruota da ora fino a HitEnd
                if (debugLogs) Debug.Log($"[Assist] Target lock: {assistTarget.name}");
            }
        }

        StopBlendOut();
        StopLayerLerp();
        SetAttackLayerWeightInstant(1f);
        SetAttacking(true);

        if (playerMove) playerMove.SetExternalSpeedMultiplier(0.55f); // windupSpeedMul
        StartCoroutine(AimAssistLock(0.10f)); // breve lock rotazione lato locomotion
    }

    private void OnHitStart()
    {
        assistActivePhase = true; // da qui “Active lock”
        if (playerMove) playerMove.SetExternalSpeedMultiplier(0.80f);

        // Se NON abbiamo assist, esegui micro-lunge standard
        if (assistTarget == null)
        {
            if (lungeDistance > 0f && lungeDuration > 0f)
                StartCoroutine(DoLunge(lungeDistance, lungeDuration));
        }

        if (hitbox) { hitbox.BeginSwing(); hitbox.SetActive(true); }
    }

    private void OnHitEnd()
    {
        assistActivePhase = false; // ferma fase active
        StopAssist();              // chiudi l’advance assistito

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
            StartBlendOut(0.18f, 0.03f); // blendOutTimeA1
            if (comboStep == 1 || comboStep == 2) EndChain(false);
        }
        else
        {
            StopBlendOut();
            SetAttackLayerWeightInstant(1f);
            if (debugLogs) Debug.Log("[Combat] RecoverStart (combo incoming) → keep layer UP");
        }
    }

    private void OnComboOpen()
    {
        comboWindowOpen = true;

        // Micro-grace per A1 → A2
        if (comboStep == 1 && preComboPressedA1 && (Time.time - preComboPressedAt) <= comboPreOpenGraceA1)
        {
            comboQueuedA2 = true;
            if (hasNextComboParam) animator.SetBool(nextComboParam, true);
            nextComboSetAt = Time.time;
            if (debugLogs) Debug.Log("[Combat] Combo A2 QUEUED (micro-grace A1)");
        }
        preComboPressedA1 = false;

        if (debugLogs) Debug.Log("[Combat] Combo window OPEN");
    }

    private void OnComboClose()
    {
        comboWindowOpen = false;
        if (debugLogs) Debug.Log("[Combat] Combo window CLOSE");
    }

    public void AttackClipEnd() { /* decisione a 0.98 in Update() */ }

    // ===================== COMBO / TRANSIZIONI =====================
    private void ForceA2()
    {
        comboQueuedA2 = false;
        StopBlendOut();
        StopLayerLerp();
        SetAttackLayerWeightInstant(1f);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        nextComboSetAt = -999f;

        comboStep = 2;
        SetAttacking(true);

        // ====== G) nuova finestra assist su A2 ======
        attackStartTime = Time.time;
        assistLockedThisAttack = false;
        assistTarget = null;
        assistActivePhase = false;
        StopAssist();

        animator.CrossFadeInFixedTime(attack2StateName, 0.05f, attackLayerIndex, 0f);
        if (debugLogs) Debug.Log("[Combat] FORCE A2");
    }

    // ===================== FINE CATENA =====================
    private void EndChainSmooth(bool fromA2)
    {
        comboStep = 0;
        comboQueuedA2 = false;
        comboWindowOpen = false;

        SetAttacking(false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);
        nextComboSetAt = -999f;

        nextAttackAllowedAt = Time.time; // sblocco immediato
        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

        StartBlendOut(fromA2 ? 0.24f : 0.18f, 0.03f); // blendOutTimeA2/A1

        if (!string.IsNullOrEmpty(attackLayerIdleState))
            animator.Play(attackLayerIdleState, attackLayerIndex, 0f);

        StartCoroutine(ForceUnlockNextFrame());
        StopAssist();
        if (debugLogs) Debug.Log("[Combat] Chain END → ready immediately");
    }

    private void EndChain(bool forceWeightZero = true)
    {
        comboStep = 0;
        comboQueuedA2 = false;
        comboWindowOpen = false;

        SetAttacking(false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);
        nextComboSetAt = -999f;

        nextAttackAllowedAt = Time.time;
        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

        if (forceWeightZero) SetAttackLayerWeightInstant(0f);
        if (!string.IsNullOrEmpty(attackLayerIdleState))
            animator.Play(attackLayerIdleState, attackLayerIndex, 0f);

        StartCoroutine(ForceUnlockNextFrame());
        StopAssist();
        if (debugLogs) Debug.Log("[Combat] Chain END (hard) → ready immediately");
    }

    private IEnumerator ForceUnlockNextFrame()
    {
        yield return null;
        ForceClearAttacking();
    }

    private void ForceClearAttacking()
    {
        isAttacking = false;
        if (hasIsAttackingParam) animator.SetBool(isAttackingParam, false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);
    }

    private void HardResetFlags()
    {
        StopBlendOut();
        SetAttackLayerWeightInstant(0f);
        comboStep = 0;
        comboQueuedA2 = false;
        comboWindowOpen = false;
        nextComboSetAt = -999f;
        ForceClearAttacking();
        nextAttackAllowedAt = Time.time;
        StopAssist();
    }

    // ===================== ASSIST: Acquire / Advance =====================
    private Transform AcquireAssistTarget(bool isHeavy)
    {
        // Se ho uno sticky valido, provalo prima
        if (stickyTarget && Time.time <= stickyUntil && IsCandidateValid(stickyTarget, isHeavy))
            return stickyTarget;

        float maxAngle = isHeavy ? assistAngleHeavy : assistAngleLight; // angolo totale
        float halfCone = Mathf.Max(1f, maxAngle * 0.5f);

        Collider[] cols = Physics.OverlapSphere(transform.position, assistRange, enemyMask, QueryTriggerInteraction.Ignore);
        if (cols == null || cols.Length == 0) return null;

        Transform best = null;
        float bestScore = -1f;

        Vector3 origin = GetRayOrigin();
        Vector3 fwd = transform.forward;

        foreach (var c in cols)
        {
            if (!c || (!string.IsNullOrEmpty(enemyTag) && !c.CompareTag(enemyTag))) continue;

            Vector3 center = c.bounds.center;
            Vector3 to = center - transform.position;
            Vector3 toFlat = to; toFlat.y = 0f;

            float dist = toFlat.magnitude;
            if (dist < 0.5f || dist > assistRange) continue; // range 0.5–2.5

            float dy = Mathf.Abs(to.y);
            if (dy > heightTolerance) continue; // quota

            float angle = Vector3.Angle(fwd, toFlat);
            if (angle > halfCone) continue; // cono

            // Line of Sight: raycast pulito
            Vector3 toCenter = center - origin;
            if (Physics.Raycast(origin, toCenter.normalized, out RaycastHit hit, toCenter.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != c) continue; // ostacolo in mezzo
            }

            // Scoring: 0.6*(1 - dist/Rmax) + 0.3*cos(angle) + 0.1*sticky
            float distTerm = 1f - (dist / assistRange);
            float angleTerm = Mathf.Cos(angle * Mathf.Deg2Rad); // [0..1]
            float stickyTerm = (stickyTarget && c.transform == stickyTarget && Time.time <= stickyUntil) ? 0.1f : 0f;
            float score = 0.6f * distTerm + 0.3f * angleTerm + stickyTerm;

            if (score >= 0.4f && score > bestScore)
            {
                bestScore = score;
                best = c.transform;
            }
        }

        return best;
    }

    private bool IsCandidateValid(Transform t, bool isHeavy)
    {
        if (!t) return false;
        Vector3 to = t.position - transform.position;
        Vector3 toFlat = to; toFlat.y = 0f;

        float dist = toFlat.magnitude;
        if (dist < 0.5f || dist > assistRange) return false;

        float dy = Mathf.Abs(to.y);
        if (dy > heightTolerance) return false;

        float halfCone = (isHeavy ? assistAngleHeavy : assistAngleLight) * 0.5f;
        float angle = Vector3.Angle(transform.forward, toFlat);
        if (angle > halfCone) return false;

        // LoS
        Vector3 origin = GetRayOrigin();
        Vector3 toCenter = t.GetComponent<Collider>() ? t.GetComponent<Collider>().bounds.center - origin : (t.position - origin);
        if (Physics.Raycast(origin, toCenter.normalized, out RaycastHit hit, toCenter.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (hit.transform != t) return false;

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
        assistTarget = null;
        assistActivePhase = false;
    }

    private IEnumerator AssistAdvanceRoutine()
    {
        if (!assistTarget) yield break;

        // Calcola advance target (quanto avanzare) verso distanza ideale
        float advanceMax = maxLungeLight;
        float remaining = ComputeAdvanceDistance(assistTarget, idealHitDistance, advanceMax);

        if (remaining <= 0.001f)
            yield break; // troppo vicino → niente advance (mini-passo sarà gestito da Compute)

        // Distribuzione tra startup + active (da WindupStart a HitEnd)
        // Qui non abbiamo la durata esatta: usiamo un easing temporale finché non arriva HitEnd.
        float eased = 0f;
        float speed = remaining / 0.18f; // stima default: 0.18s fino all'impatto; si adatta coi clamp
        Vector3 lastPos = transform.position;

        while (assistTarget && (isAttacking || assistActivePhase))
        {
            // Slerp rotazione: startup segui target a turnSpeed, in active lock (già attivo via assistActivePhase)
            Vector3 to = (assistTarget.position - transform.position);
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(to.normalized, Vector3.up);
                float stepDeg = turnSpeedDegPerSec * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, stepDeg);
            }

            if (remaining > 0f)
            {
                // Ease in-out sulla quantità rimanente
                float dt = Time.deltaTime;
                float rawStep = speed * dt;
                float tNorm = Mathf.Clamp01(eased / Mathf.Max(remaining, 0.0001f));
                float ease = EaseInOutCubic(1f - tNorm); // più deciso all'inizio, si smorza verso la fine
                float step = Mathf.Min(remaining, rawStep * Mathf.Lerp(0.6f, 1.2f, ease));

                Vector3 delta = transform.forward * step;

                // Collision handling: pre-raycast per troncare e leggera “proiezione” lungo la normale
                if (Physics.Raycast(GetRayOrigin(), delta.normalized, out RaycastHit hit, step + 0.05f, ~0, QueryTriggerInteraction.Ignore))
                {
                    // Tronca alla distanza utile
                    float allowed = Mathf.Max(0f, hit.distance - 0.02f);
                    Vector3 tryMove = delta.normalized * allowed;

                    // Proiezione (slide) approssimata: rimuovi componente verso la normale, tieni tangenziale
                    Vector3 slide = Vector3.ProjectOnPlane(delta - tryMove, hit.normal);
                    cc.Move(tryMove + slide * 0.25f);
                    remaining -= allowed;
                    eased += allowed;
                }
                else
                {
                    cc.Move(delta);
                    remaining -= step;
                    eased += step;
                }

                lastPos = transform.position;
            }

            // termina se abbiamo esaurito l'advance
            if (remaining <= 0.0001f) break;

            yield return null;
        }
        assistCR = null;
    }

    private float ComputeAdvanceDistance(Transform target, float ideal, float maxAdvance)
    {
        if (!target) return 0f;
        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;
        float advance = Mathf.Clamp(dist - ideal, 0f, maxAdvance);

        // Mini-passo se troppo vicino (<0.4 m) → 0..0.3 m
        if (dist < 0.4f) advance = Mathf.Min(advance, 0.3f);
        return advance;
    }

    private Vector3 GetRayOrigin()
    {
        // Origine ragionevole per i ray (petto)
        Vector3 o = transform.position;
        o.y += 1.2f;
        return o;
    }

    private static float EaseInOutCubic(float x)
    {
        // 0..1
        return (x < 0.5f) ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

    // ===================== Lunge fallback / Aim lock =====================
    private IEnumerator DoLunge(float distance, float duration)
    {
        if (distance <= 0f || duration <= 0f) yield break;
        float moved = 0f;
        while (moved < distance)
        {
            float step = (distance / duration) * Time.deltaTime;
            Vector3 delta = transform.forward * step;
            cc.Move(delta);
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
