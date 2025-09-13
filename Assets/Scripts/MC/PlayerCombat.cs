using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;               // L'UNICO Animator con MC_Animator_v01
    [SerializeField] private CombatEventsBridge eventsBridge; // Sullo stesso GO dell'Animator
    [SerializeField] private CharacterController cc;
    [SerializeField] private PlayerController playerMove;     // MovementOnly (anim opzionale)
    [SerializeField] private Hitbox hitbox;
    [SerializeField] private CameraJolt cameraJolt;
    [SerializeField] private Hitstopper hitstopper;
    [SerializeField] private PlayerInput input;

    [Header("Animator params / layer")]
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string nextComboParam = "NextCombo";
    [SerializeField] private string isAttackingParam = "IsAttacking";
    [SerializeField] private int attackLayerIndex = 1;        // "Attack_UpperBody"

    [Header("State names (Layer Attack)")]
    [SerializeField] private string attack1StateName = "Light_A1";
    [SerializeField] private string attack2StateName = "Light_A2";
    [SerializeField] private string attackLayerIdleState = ""; // opzionale (es. "AttackLayer_Idle")

    [Header("Combo & Buffer")]
    [SerializeField] private float inputBufferSeconds = 0.20f; // (non usato per re-attack ora, ma teniamolo)
    private float lastAttackPressed = -999f;
    private float nextAttackAllowedAt = 0f; // anti-spam tra catene

    private bool isAttacking;
    private int comboStep = 0;            // 0=none, 1=A1, 2=A2
    private bool comboWindowOpen = false;
    private bool comboQueued = false;

    [Header("Aim Assist & Lock")]
    [SerializeField] private float aimAssistMaxAngle = 15f;
    [SerializeField] private float aimAssistRange = 2.5f;
    [SerializeField] private float aimLockDuration = 0.10f;
    [SerializeField] private LayerMask enemyMask = ~0;
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Slow & Lunge")]
    [SerializeField] private float windupSpeedMul = 0.55f;
    [SerializeField] private float hitSpeedMul = 0.80f;
    [SerializeField] private float recoverSpeedMul = 1.0f;
    [SerializeField] private float lungeDistance = 1.1f;
    [SerializeField] private float lungeDuration = 0.085f;

    [Header("Hitbox & Feedback")]
    [SerializeField] private float hitstopSeconds = 0.05f;
    [SerializeField] private float cameraJoltSeconds = 0.02f;

    // ---------------- LAYER WEIGHT DRIVER (nuovo) ----------------
    [Header("Layer Weight Tuning")]
    [SerializeField, Tooltip("Tempo SmoothDamp per salire a 1 all’inizio.")]
    private float weightSmoothUp = 0.06f;
    [SerializeField, Tooltip("Target dopo HitEnd (passaggio morbido verso recover).")]
    private float weightAfterHitEnd = 0.90f;
    [SerializeField, Tooltip("Tempo SmoothDamp per scendere a weightAfterHitEnd.")]
    private float weightAfterHitEndSmooth = 0.12f;
    [SerializeField, Tooltip("Target a RecoverStart (ancora controllo braccia).")]
    private float weightDuringRecover = 0.55f;
    [SerializeField, Tooltip("Tempo SmoothDamp per scendere a weightDuringRecover.")]
    private float weightDuringRecoverSmooth = 0.16f;
    [SerializeField, Tooltip("Peso tenuto per un attimo dopo la fine clip per evitare pop.")]
    private float postEndHoldWeight = 0.35f;
    [SerializeField, Tooltip("Attendi questo tempo prima di entrare nell'hold post-fine.")]
    private float postEndHoldDelay = 0.02f;
    [SerializeField, Tooltip("Durata dell'hold post-fine prima del rilascio a 0.")]
    private float postEndHoldDuration = 0.12f;
    [SerializeField, Tooltip("Tempo SmoothDamp per rilasciare da hold a 0.")]
    private float weightReleaseSmooth = 0.22f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // Input
    private const string MAP = "Gameplay";
    private const string ATTACK = "Attack";

    // Runtime
    private int attackTagHash, a1ShortHash, a2ShortHash;
    private bool hasIsAttackingParam, hasNextComboParam, hasAttackTrigger;

    // Weight driver runtime
    private float weightTarget = 0f;
    private float weightSmoothTime = 0.1f;
    private float weightVel = 0f;
    private Coroutine postEndCR;

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
        // --- Weight driver (SmoothDamp verso il target corrente) ---
        float w = animator.GetLayerWeight(attackLayerIndex);
        float newW = Mathf.SmoothDamp(w, weightTarget, ref weightVel, Mathf.Max(0.01f, weightSmoothTime), Mathf.Infinity, Time.deltaTime);
        if (!Mathf.Approximately(newW, w))
            animator.SetLayerWeight(attackLayerIndex, newW);

        // --- Stato Animator ---
        var st = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        bool inAttackTag = (st.tagHash == attackTagHash);

        // Autodetect ingresso in A2
        if (st.shortNameHash == a2ShortHash && comboStep != 2)
        {
            comboStep = 2;
            if (hasNextComboParam) animator.SetBool(nextComboParam, false);
            SetAttacking(true);
            // tieni peso alto durante A2
            SetLayerTarget(1f, weightSmoothUp);
            if (debugLogs) Debug.Log("[Combat] Enter A2 → comboStep=2");
        }

        // Safety: se stai in Attack ma per qualche motivo il target fosse basso, riallinealo
        if (inAttackTag && weightTarget < 0.95f)
            SetLayerTarget(1f, weightSmoothUp);

        // Fine A1: decidi combo o chiusura
        if (comboStep == 1 &&
            st.shortNameHash == a1ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            bool shouldCombo = comboQueued || (hasNextComboParam && animator.GetBool(nextComboParam));
            if (shouldCombo)
            {
                comboQueued = false;
                if (hasNextComboParam) animator.SetBool(nextComboParam, false);
                comboStep = 2;
                animator.CrossFadeInFixedTime(attack2StateName, 0.05f, attackLayerIndex, 0f);
                SetLayerTarget(1f, weightSmoothUp);
                if (debugLogs) Debug.Log("[Combat] A1 finished → force CrossFade to A2");
            }
            else
            {
                if (debugLogs) Debug.Log("[Combat] A1 finished (no combo) → smooth end");
                EndChainSmooth(false);
            }
        }

        // Fine A2 → chiudi catena con blend-out morbido
        if (comboStep == 2 &&
            st.shortNameHash == a2ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            if (debugLogs) Debug.Log("[Combat] A2 finished → smooth end");
            EndChainSmooth(true);
        }

        // Failsafe: se non stai attaccando e il target fosse rimasto alto (rari casi), punta a 0
        if (!isAttacking && weightTarget > 0.001f && postEndCR == null)
            SetLayerTarget(0f, weightReleaseSmooth);
    }

    // ================= INPUT =================
    private void OnAttackPressed(InputAction.CallbackContext ctx)
    {
        lastAttackPressed = Time.time;

        if (!isAttacking)
        {
            TryStartAttack1();
            return;
        }

        if (isAttacking && comboStep == 1 && comboWindowOpen)
        {
            comboQueued = true;
            if (debugLogs) Debug.Log("[Combat] Combo QUEUED (press in window)");
        }
    }

    private bool CanStartAttack1() => Time.time >= nextAttackAllowedAt;

    private void TryStartAttack1()
    {
        if (!CanStartAttack1()) return;

        // Interrompi eventuale rilascio post-fine
        if (postEndCR != null) { StopCoroutine(postEndCR); postEndCR = null; }

        // Pre-raise
        SetLayerTarget(1f, weightSmoothUp);
        SetAttacking(true);

        // Prep
        comboStep = 1;
        comboQueued = false;
        comboWindowOpen = false;
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);

        // Entrata sicura in A1
        if (!string.IsNullOrEmpty(attack1StateName))
            animator.CrossFadeInFixedTime(attack1StateName, 0.05f, attackLayerIndex, 0f);
        else if (hasAttackTrigger)
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }

        if (debugLogs) Debug.Log("[Combat] Attack1 start");
    }

    // ================= EVENTI CLIP =================
    private void OnWindupStart()
    {
        SetLayerTarget(1f, weightSmoothUp);
        SetAttacking(true);
        if (playerMove) playerMove.SetExternalSpeedMultiplier(windupSpeedMul);
        StartCoroutine(AimAssistLock(aimLockDuration));
    }

    private void OnHitStart()
    {
        if (playerMove) playerMove.SetExternalSpeedMultiplier(hitSpeedMul);
        if (lungeDistance > 0f && lungeDuration > 0f) StartCoroutine(DoLunge(lungeDistance, lungeDuration));
        if (hitbox) { hitbox.BeginSwing(); hitbox.SetActive(true); }
    }

    private void OnHitEnd()
    {
        // Inizia a cedere un filo il layer
        SetLayerTarget(weightAfterHitEnd, weightAfterHitEndSmooth);
        if (hitbox) hitbox.SetActive(false);
        if (hitstopper) hitstopper.DoHitstop(hitstopSeconds);
        if (cameraJolt) cameraJolt.DoJolt(cameraJoltSeconds);
    }

    private void OnRecoverStart()
    {
        if (playerMove) playerMove.SetExternalSpeedMultiplier(recoverSpeedMul);
        // Cedi ancora un po’ il layer per mano al base layer, ma senza staccare di colpo
        SetLayerTarget(weightDuringRecover, weightDuringRecoverSmooth);
    }

    private void OnComboOpen()
    {
        comboWindowOpen = true;
        if (debugLogs) Debug.Log("[Combat] Combo window OPEN");
    }

    private void OnComboClose()
    {
        // Chiudi la finestra; se abbiamo input, abilita NextCombo per chi usa anche la transizione da Animator
        comboWindowOpen = false;
        if (comboStep == 1 && comboQueued)
        {
            if (hasNextComboParam) animator.SetBool(nextComboParam, true);
            if (debugLogs) Debug.Log("[Combat] Combo queued → NextCombo TRUE (wait/end)");
        }
    }

    public void AttackClipEnd() { /* non usato: finiamo in Update() a 0.98 */ }

    // ================= FINE CATENA (morbida) =================
    private void EndChainSmooth(bool fromA2)
    {
        comboStep = 0;
        SetAttacking(false);

        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);

        nextAttackAllowedAt = Time.time + 0.35f;
        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

        // Post-fine: piccola attesa, hold a 0.35, poi rilascio a 0 con SmoothDamp
        if (postEndCR != null) StopCoroutine(postEndCR);
        postEndCR = StartCoroutine(PostEndReleaseRoutine());

        if (!string.IsNullOrEmpty(attackLayerIdleState))
            animator.Play(attackLayerIdleState, attackLayerIndex, 0f);

        if (debugLogs) Debug.Log("[Combat] Chain END → smooth unlock");
    }

    private IEnumerator PostEndReleaseRoutine()
    {
        // breve delay prima dell'hold per far “respirare” l'ultima posa
        if (postEndHoldDelay > 0f) yield return new WaitForSeconds(postEndHoldDelay);

        // tieni un minimo di controllo upper-body
        SetLayerTarget(postEndHoldWeight, 0.10f);
        if (postEndHoldDuration > 0f) yield return new WaitForSeconds(postEndHoldDuration);

        // poi rilascia dolcemente a 0
        SetLayerTarget(0f, weightReleaseSmooth);
        postEndCR = null;
    }

    // ================= Weight driver helper =================
    private void SetLayerTarget(float target, float smoothTime)
    {
        weightTarget = Mathf.Clamp01(target);
        weightSmoothTime = Mathf.Max(0.01f, smoothTime);
        // Nota: l'attuale valore scivolerà verso il target in Update() via SmoothDamp
    }

    // ================= Stato / utils vari =================
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

        float t = 0f;
        while (t < duration) { t += Time.deltaTime; yield return null; }

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
