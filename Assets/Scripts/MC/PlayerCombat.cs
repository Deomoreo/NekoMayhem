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
    [SerializeField] private PlayerController playerMove;
    [SerializeField] private Hitbox hitbox;
    [SerializeField] private CameraJolt cameraJolt;
    [SerializeField] private Hitstopper hitstopper;
    [SerializeField] private PlayerInput input;

    [Header("Animator params / layer")]
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string nextComboParam = "NextCombo";
    [SerializeField] private string isAttackingParam = "IsAttacking";
    [SerializeField] private int attackLayerIndex = 1;
    [SerializeField] private float layerLerpUp = 18f;
    [SerializeField] private float layerLerpDown = 10f;

    [Header("Stati (Layer Attack)")]
    [SerializeField] private string attack1StateName = "Light_A1";
    [SerializeField] private string attack2StateName = "Light_A2";
    [SerializeField] private string attack3StateName = "Light_A3";   // A3
    [SerializeField] private string attackLayerIdleState = ""; // opzionale

    // Nessun cooldown forzato dopo fine catena
    private float nextAttackAllowedAt = 0f;

    // Stato/combo
    private bool isAttacking;
    // 0=none, 1=A1, 2=A2, 3=A3
    private int comboStep = 0;
    private bool comboWindowOpen = false;
    private bool comboQueuedA2 = false;  // premuto durante finestra di A1 → A2
    private bool comboQueuedA3 = false;  // premuto durante finestra di A2 → A3

    [Header("Early-Restart (solo capi catena)")]
    [SerializeField, Tooltip("Se stai in A2 oltre questa normalized time e la finestra combo NON è aperta, un click fa ripartire A1.")]
    private float a2RestartThresholdNormalized = 0.96f;
    [SerializeField, Tooltip("Se stai in A3 oltre questa normalized time, un click fa ripartire A1.")]
    private float a3RestartThresholdNormalized = 0.96f; // A3

    [Header("Combo micro-grace (NO hold, NO buffer globale)")]
    [SerializeField, Tooltip("Premi ≤X secondi PRIMA della ComboOpen di A1 → la combo A2 viene accettata.")]
    private float comboPreOpenGraceA1 = 0.08f;
    [SerializeField, Tooltip("Premi ≤X secondi PRIMA della ComboOpen di A2 → la combo A3 viene accettata.")]
    private float comboPreOpenGraceA2 = 0.08f; // A3
    private bool preComboPressedA1 = false;
    private bool preComboPressedA2 = false;   // A3
    private float preComboPressedAt = -999f;

    [Header("Aim/Lunge base")]
    [SerializeField] private float aimAssistMaxAngle = 15f;
    [SerializeField] private float aimAssistRange = 2.5f;
    [SerializeField] private float aimLockDuration = 0.10f;
    [SerializeField] private LayerMask enemyMask = ~0;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float windupSpeedMul = 0.55f;
    [SerializeField] private float hitSpeedMul = 0.80f;
    [SerializeField] private float recoverSpeedMul = 1.0f;

    [Header("Lunge A1/A2")]
    [SerializeField] private float lungeDistance = 1.1f;
    [SerializeField] private float lungeDuration = 0.085f;

    [Header("Affondo A3 (condizionato)")]
    [SerializeField, Tooltip("Spinta in avanti dell'affondo (se non siamo troppo vicini).")]
    private float thrustDistance = 2.2f;                 // distanza affondo
    [SerializeField, Tooltip("Durata della spinta dell'affondo.")]
    private float thrustDuration = 0.10f;                // durata affondo
    [SerializeField, Tooltip("Se il nemico frontale è più vicino di questa soglia, NON affondare.")]
    private float thrustNoLungeRange = 1.0f;             // no-lunge se molto vicino
    [SerializeField, Tooltip("Raggio di ricerca per valutare distanza nemico frontale.")]
    private float thrustCheckRange = 2.8f;               // ricerca
    [SerializeField, Tooltip("Angolo massimo rispetto al forward per considerare 'frontale'.")]
    private float thrustCheckMaxAngle = 35f;             // cono frontale

    [Header("Hitbox & Feedback")]
    [SerializeField] private float hitstopSeconds = 0.05f;
    [SerializeField] private float cameraJoltSeconds = 0.02f;

    [Header("Blend-out morbido")]
    [SerializeField] private float blendOutDelay = 0.03f;
    [SerializeField] private float blendOutTimeA1 = 0.18f;
    [SerializeField] private float blendOutTimeA2 = 0.24f;
    [SerializeField] private float blendOutTimeA3 = 0.26f;  // A3 leggermente più lungo

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // Runtime
    private Coroutine layerCR, blendOutCR;
    private int attackTagHash, a1ShortHash, a2ShortHash, a3ShortHash;
    private bool hasIsAttackingParam, hasNextComboParam, hasAttackTrigger;

    // Input
    private const string MAP = "Gameplay";
    private const string ATTACK = "Attack";

    // Watchdog anti-stallo NextCombo
    private float nextComboSetAt = -999f;
    private const float nextComboMaxHang = 0.6f;

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!eventsBridge) eventsBridge = animator ? animator.GetComponent<CombatEventsBridge>() : null;
        if (!cc) cc = GetComponent<CharacterController>();
        if (!playerMove) playerMove = GetComponent<PlayerController>();
        if (!input) input = GetComponent<PlayerInput>();

        attackTagHash = Animator.StringToHash("Attack");
        a1ShortHash = Animator.StringToHash(attack1StateName);
        a2ShortHash = Animator.StringToHash(attack2StateName);
        a3ShortHash = Animator.StringToHash(attack3StateName);

        hasIsAttackingParam = HasAnimatorBool(animator, isAttackingParam);
        hasNextComboParam = HasAnimatorBool(animator, nextComboParam);
        hasAttackTrigger = HasAnimatorTrigger(animator, attackTrigger);
    }

    private void OnEnable()
    {
        var map = input.actions.FindActionMap(MAP, true);
        map.FindAction(ATTACK, true).performed += OnAttackPressed;

        if (eventsBridge)
        {
            eventsBridge.OnWindupStart += OnWindupStart;
            eventsBridge.OnHitStart += OnHitStart;
            eventsBridge.OnHitEnd += OnHitEnd;
            eventsBridge.OnRecoverStart += OnRecoverStart;
            eventsBridge.OnComboOpen += OnComboOpen;
            eventsBridge.OnComboClose += OnComboClose;
        }
    }

    private void OnDisable()
    {
        var map = input.actions.FindActionMap(MAP, false);
        if (map != null)
            map.FindAction(ATTACK, false).performed -= OnAttackPressed;

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
            if (debugLogs) Debug.Log("[Combat] Enter A2 → comboStep=2");
        }

        // Autodetect ingresso A3
        if (st.shortNameHash == a3ShortHash && comboStep != 3)
        {
            comboStep = 3;
            if (hasNextComboParam) animator.SetBool(nextComboParam, false);
            nextComboSetAt = -999f;
            SetAttacking(true);
            StopBlendOut();
            if (debugLogs) Debug.Log("[Combat] Enter A3 → comboStep=3");
        }

        // Safety: in tag Attack tieni il layer alto
        if (inAttackTag && w < 0.95f) SetAttackLayerWeight(1f);

        // FINE A1 → A2 se queue; altrimenti end
        if (comboStep == 1 &&
            st.shortNameHash == a1ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            if (comboQueuedA2 || (hasNextComboParam && animator.GetBool(nextComboParam)))
            {
                ForceA2();
            }
            else
            {
                if (debugLogs) Debug.Log("[Combat] A1 finished (no combo) → end");
                EndChainSmooth(false);
            }
        }

        // FINE A2 → A3 se queue; altrimenti end
        if (comboStep == 2 &&
            st.shortNameHash == a2ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            if (comboQueuedA3 || (hasNextComboParam && animator.GetBool(nextComboParam)))
            {
                ForceA3();
            }
            else
            {
                if (debugLogs) Debug.Log("[Combat] A2 finished (no combo) → end");
                EndChainSmooth(false);
            }
        }

        // FINE A3 → end catena
        if (comboStep == 3 &&
            st.shortNameHash == a3ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            if (debugLogs) Debug.Log("[Combat] A3 finished → end");
            EndChainSmooth(true);
        }

        // Watchdog: NextCombo non deve restare appeso
        if (hasNextComboParam && animator.GetBool(nextComboParam))
        {
            if (nextComboSetAt < 0f) nextComboSetAt = Time.time;

            bool inComboStates = (st.shortNameHash == a1ShortHash) || (st.shortNameHash == a2ShortHash) || (st.shortNameHash == a3ShortHash);
            if (!inComboStates && !animator.IsInTransition(attackLayerIndex))
            {
                if (debugLogs) Debug.LogWarning("[Combat] NEXT stuck fuori da Attack → reset");
                HardResetFlags();
            }
            else if ((comboStep == 1 || comboStep == 2) && Time.time - nextComboSetAt > nextComboMaxHang)
            {
                if (debugLogs) Debug.LogWarning("[Combat] NEXT appeso troppo → FORCE next");
                if (comboStep == 1) ForceA2(); else ForceA3();
            }
        }
        else nextComboSetAt = -999f;

        // se non attacco e weight su → abbassa
        if (!isAttacking && w > 0.01f) SetAttackLayerWeight(0f);

        // failsafe
        if (isAttacking && !inAttackTag && w <= 0.01f)
            ForceClearAttacking();
    }

    // =============== INPUT ===============
    private void OnAttackPressed(InputAction.CallbackContext ctx)
    {
        // 1) Sei in A2 e la finestra è APERTA → priorità alla COMBO (A3), NON early-restart
        if (isAttacking && comboStep == 2 && comboWindowOpen)
        {
            comboQueuedA3 = true;
            if (hasNextComboParam) animator.SetBool(nextComboParam, true);
            nextComboSetAt = Time.time;
            if (debugLogs) Debug.Log("[Combat] Combo A3 QUEUED (press in A2 window)");
            return;
        }

        // 2) Early-restart SOLO se la finestra NON è aperta:
        //    - fine A2 → riparti A1
        //    - fine A3 → riparti A1
        if (!comboWindowOpen && (IsAtEndOfA2() || IsAtEndOfA3()))
        {
            TryStartAttack1();
            return;
        }

        // 3) Primo click: se non stai attaccando → A1
        if (!isAttacking)
        {
            TryStartAttack1();
            return;
        }

        // 4) Sei in A1
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
                // Micro-grace per pre-open A1
                preComboPressedA1 = true;
                preComboPressedAt = Time.time;
                if (debugLogs) Debug.Log("[Combat] Pre-Combo A2 micro-grace armed");
            }
            return;
        }

        // 5) Sei in A2 ma finestra CHIUSA (qui non è early-restart e non sei oltre soglia) → arma micro-grace A3
        if (isAttacking && comboStep == 2)
        {
            preComboPressedA2 = true;
            preComboPressedAt = Time.time;
            if (debugLogs) Debug.Log("[Combat] Pre-Combo A3 micro-grace armed");
            return;
        }

        // altri casi → ignoriamo
    }

    private bool CanStartAttack1() => Time.time >= nextAttackAllowedAt;

    private void TryStartAttack1()
    {
        if (!CanStartAttack1()) return;

        StopBlendOut();

        SetAttackLayerWeight(1f);
        SetAttacking(true);

        comboStep = 1;
        comboQueuedA2 = false;
        comboQueuedA3 = false;
        comboWindowOpen = false;
        preComboPressedA1 = false;
        preComboPressedA2 = false;
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        nextComboSetAt = -999f;

        if (!string.IsNullOrEmpty(attack1StateName))
            animator.CrossFadeInFixedTime(attack1StateName, 0.05f, attackLayerIndex, 0f);
        else if (hasAttackTrigger)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }

        if (debugLogs) Debug.Log("[Combat] Attack1 start");
    }

    // =============== EVENTI CLIP ===============
    private void OnWindupStart()
    {
        SetAttackLayerWeight(1f);
        SetAttacking(true);
        if (playerMove) playerMove.SetExternalSpeedMultiplier(windupSpeedMul);
        StartCoroutine(AimAssistLock(aimLockDuration));
    }

    private void OnHitStart()
    {
        if (playerMove) playerMove.SetExternalSpeedMultiplier(hitSpeedMul);

        // Lunge diverso per A3
        if (comboStep == 3)
        {
            float d = ComputeThrustDistance(); // 0 se vicino → niente affondo
            if (d > 0f && thrustDuration > 0f)
                StartCoroutine(DoLunge(d, thrustDuration));
        }
        else
        {
            if (lungeDistance > 0f && lungeDuration > 0f)
                StartCoroutine(DoLunge(lungeDistance, lungeDuration));
        }

        if (hitbox) { hitbox.BeginSwing(); hitbox.SetActive(true); }
    }

    private void OnHitEnd()
    {
        if (hitbox) hitbox.SetActive(false);
        if (hitstopper) hitstopper.DoHitstop(hitstopSeconds);
        if (cameraJolt) cameraJolt.DoJolt(cameraJoltSeconds);
    }

    private void OnRecoverStart()
    {
        if (playerMove) playerMove.SetExternalSpeedMultiplier(recoverSpeedMul);

        // Se NON c’è combo in arrivo → blend-out + chiusura
        bool comboIncoming =
            (comboStep == 1 && (comboQueuedA2 || (hasNextComboParam && animator.GetBool(nextComboParam)))) ||
            (comboStep == 2 && (comboQueuedA3 || (hasNextComboParam && animator.GetBool(nextComboParam))));

        if (!comboIncoming)
        {
            float t = (comboStep == 3) ? blendOutTimeA3 : (comboStep == 2 ? blendOutTimeA2 : blendOutTimeA1);
            StartBlendOut(t, blendOutDelay);

            if (comboStep == 1 || comboStep == 2 || comboStep == 3)
                EndChain(false);
        }
        else
        {
            StopBlendOut();
            SetAttackLayerWeight(1f);
            if (debugLogs) Debug.Log("[Combat] RecoverStart (combo incoming) → keep layer UP");
        }
    }

    private void OnComboOpen()
    {
        comboWindowOpen = true;

        // MICRO-GRACE: consumo eventuale pre-press
        if (comboStep == 1 && preComboPressedA1 && (Time.time - preComboPressedAt) <= comboPreOpenGraceA1)
        {
            comboQueuedA2 = true;
            if (hasNextComboParam) animator.SetBool(nextComboParam, true);
            nextComboSetAt = Time.time;
            if (debugLogs) Debug.Log("[Combat] Combo A2 QUEUED (micro-grace A1)");
        }
        if (comboStep == 2 && preComboPressedA2 && (Time.time - preComboPressedAt) <= comboPreOpenGraceA2)
        {
            comboQueuedA3 = true;
            if (hasNextComboParam) animator.SetBool(nextComboParam, true);
            nextComboSetAt = Time.time;
            if (debugLogs) Debug.Log("[Combat] Combo A3 QUEUED (micro-grace A2)");
        }

        preComboPressedA1 = false;
        preComboPressedA2 = false;

        if (debugLogs) Debug.Log("[Combat] Combo window OPEN");
    }

    private void OnComboClose()
    {
        comboWindowOpen = false;
        if (debugLogs) Debug.Log("[Combat] Combo window CLOSE");
        // decisione finale a 0.98 in Update()
    }

    public void AttackClipEnd() { /* decisione a 0.98 in Update() */ }

    // =============== COMBO / TRANSIZIONI ===============
    private void ForceA2()
    {
        comboQueuedA2 = false;
        preComboPressedA1 = false;
        StopBlendOut();
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        nextComboSetAt = -999f;

        comboStep = 2;
        SetAttackLayerWeight(1f);
        SetAttacking(true);
        animator.CrossFadeInFixedTime(attack2StateName, 0.05f, attackLayerIndex, 0f);

        if (debugLogs) Debug.Log("[Combat] FORCE A2");
    }

    private void ForceA3() // A3
    {
        comboQueuedA3 = false;
        preComboPressedA2 = false;
        StopBlendOut();
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        nextComboSetAt = -999f;

        comboStep = 3;
        SetAttackLayerWeight(1f);
        SetAttacking(true);
        animator.CrossFadeInFixedTime(attack3StateName, 0.05f, attackLayerIndex, 0f);

        if (debugLogs) Debug.Log("[Combat] FORCE A3 (thrust)");
    }

    // =============== FINE CATENA ===============
    private void EndChainSmooth(bool fromLast)
    {
        comboStep = 0;
        comboQueuedA2 = false;
        comboQueuedA3 = false;
        comboWindowOpen = false;
        preComboPressedA1 = false;
        preComboPressedA2 = false;

        SetAttacking(false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);
        nextComboSetAt = -999f;

        // sblocco immediato per ripartire A1
        nextAttackAllowedAt = Time.time;

        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

        float t = fromLast ? blendOutTimeA3 : blendOutTimeA1; // for A2 no-combo abbiamo già usato A1 nel recover
        StartBlendOut(t, blendOutDelay);

        if (!string.IsNullOrEmpty(attackLayerIdleState))
            animator.Play(attackLayerIdleState, attackLayerIndex, 0f);

        StartCoroutine(ForceUnlockNextFrame());
        if (debugLogs) Debug.Log("[Combat] Chain END → ready immediately");
    }

    private void EndChain(bool forceWeightZero = true)
    {
        comboStep = 0;
        comboQueuedA2 = false;
        comboQueuedA3 = false;
        comboWindowOpen = false;
        preComboPressedA1 = false;
        preComboPressedA2 = false;

        SetAttacking(false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);
        nextComboSetAt = -999f;

        nextAttackAllowedAt = Time.time;

        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

        if (forceWeightZero) SetAttackLayerWeight(0f);
        if (!string.IsNullOrEmpty(attackLayerIdleState))
            animator.Play(attackLayerIdleState, attackLayerIndex, 0f);

        StartCoroutine(ForceUnlockNextFrame());
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
        SetAttackLayerWeight(0f);
        comboStep = 0;
        comboQueuedA2 = false;
        comboQueuedA3 = false;
        comboWindowOpen = false;
        preComboPressedA1 = false;
        preComboPressedA2 = false;
        nextComboSetAt = -999f;
        ForceClearAttacking();
        nextAttackAllowedAt = Time.time;
    }

    // =============== Lunge / Thrust utils ===============
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

    private float ComputeThrustDistance() // A3
    {
        Transform best = null;
        float bestDist = Mathf.Infinity;

        // cerca nemici in un raggio
        Collider[] cols = Physics.OverlapSphere(transform.position, thrustCheckRange, enemyMask, QueryTriggerInteraction.Ignore);
        Vector3 fwd = transform.forward;

        foreach (var c in cols)
        {
            if (!string.IsNullOrEmpty(enemyTag) && !c.CompareTag(enemyTag)) continue;
            Vector3 to = c.bounds.center - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.0001f) continue;

            float angle = Vector3.Angle(fwd, to);
            if (angle > thrustCheckMaxAngle) continue; // non frontale

            if (dist < bestDist) { bestDist = dist; best = c.transform; }
        }

        // se vicino in fronte → NO affondo
        if (best && bestDist <= thrustNoLungeRange) return 0f;

        // altrimenti spingi in avanti (anche da fermo)
        return thrustDistance;
    }

    // =============== Layer weight / aim ===============
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

    private IEnumerator AimAssistLock(float duration)
    {
        if (playerMove) playerMove.SetRotationOverride(true);

        Transform target = AcquireSoftLockTarget();
        if (target)
        {
            Vector3 dir = target.position - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        float t = 0f; while (t < duration) { t += Time.deltaTime; yield return null; }
        if (playerMove) playerMove.SetRotationOverride(false);
    }

    private Transform AcquireSoftLockTarget()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, aimAssistRange, enemyMask, QueryTriggerInteraction.Ignore);
        Transform best = null; float bestAngle = Mathf.Infinity;
        Vector3 fwd = transform.forward;

        foreach (var c in cols)
        {
            if (!string.IsNullOrEmpty(enemyTag) && !c.CompareTag(enemyTag)) continue;
            Vector3 to = c.bounds.center - transform.position; to.y = 0f;
            float angle = Vector3.Angle(fwd, to);
            if (angle <= aimAssistMaxAngle && angle < bestAngle) { bestAngle = angle; best = c.transform; }
        }
        return best;
    }

    private bool IsAtEndOfA2()
    {
        var st = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        if (comboStep != 2) return false;
        if (st.shortNameHash != a2ShortHash) return false;
        if (animator.IsInTransition(attackLayerIndex)) return false;
        return st.normalizedTime >= a2RestartThresholdNormalized;
    }

    private bool IsAtEndOfA3() // A3
    {
        var st = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        if (comboStep != 3) return false;
        if (st.shortNameHash != a3ShortHash) return false;
        if (animator.IsInTransition(attackLayerIndex)) return false;
        return st.normalizedTime >= a3RestartThresholdNormalized;
    }

    // =============== Utils ===============
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
}
