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
    [SerializeField] private float layerLerpUp = 18f;
    [SerializeField] private float layerLerpDown = 8f;        // più morbido in discesa

    [Header("Stati d’attacco (Layer Attack)")]
    [SerializeField] private string attack1StateName = "Light_A1";
    [SerializeField] private string attack2StateName = "Light_A2";
    [SerializeField] private string attackLayerIdleState = ""; // opzionale (es. "AttackLayer_Idle")

    [Header("Combo & Buffer")]
    [SerializeField] private float inputBufferSeconds = 0.20f;
    private float lastAttackPressed = -999f;
    private bool isAttacking;
    private int comboStep = 0;               // 0=none, 1=A1, 2=A2
    private bool comboWindowOpen = false;
    private bool comboQueued = false;
    private float nextAttackAllowedAt = 0f;

    // Debounce finestra combo
    private int comboWindowToken = 0;
    private int comboWindowTokenProcessed = -1;

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

    [Header("Blend-out morbido (anti-pop)")]
    [SerializeField, Tooltip("Ritardo prima di iniziare a sfumare il layer durante il recover.")]
    private float blendOutDelay = 0.03f;
    [SerializeField, Tooltip("Durata sfumatura weight su Attack1 (s).")]
    private float blendOutTimeA1 = 0.18f;
    [SerializeField, Tooltip("Durata sfumatura weight su Attack2 (s).")]
    private float blendOutTimeA2 = 0.24f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Coroutine layerCR;
    private Coroutine blendOutCR;
    private int attackTagHash;
    private int a1ShortHash, a2ShortHash;

    // param existence cache
    private bool hasIsAttackingParam, hasNextComboParam, hasAttackTrigger;

    // Input
    private const string MAP = "Gameplay";
    private const string ATTACK = "Attack";

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
        var st = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        float w = animator.GetLayerWeight(attackLayerIndex);
        bool inAttackTag = (st.tagHash == attackTagHash);

        // Autodetect ingresso A2
        if (st.shortNameHash == a2ShortHash && comboStep != 2)
        {
            comboStep = 2;
            if (hasNextComboParam) animator.SetBool(nextComboParam, false);
            SetAttacking(true);
            if (debugLogs) Debug.Log("[Combat] Enter A2 → comboStep=2");
        }

        // Safety: se sei in Attack ma weight giù, rialza
        if (inAttackTag && w < 0.95f) SetAttackLayerWeight(1f);

        // Watchdog: fine attacco (no combo) → sblocca
        if (isAttacking && inAttackTag && st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex) && !(hasNextComboParam && animator.GetBool(nextComboParam)))
        {
            if (debugLogs) Debug.Log("[Combat] Watchdog end → unlock");
            EndOrChain();
        }

        // Se non attacchi e weight è su → abbassa
        if (!isAttacking && w > 0.01f) SetAttackLayerWeight(0f);

        // Failsafe: se non sei più in tag Attack e weight basso, spegni bool se fosse stuck
        if (isAttacking && !inAttackTag && w <= 0.01f)
        {
            if (debugLogs) Debug.Log("[Combat] Failsafe clear IsAttacking");
            ForceClearAttacking();
        }
    }

    // ================= INPUT =================
    private void OnAttackPressed(InputAction.CallbackContext ctx)
    {
        lastAttackPressed = Time.time;

        if (!isAttacking) { TryStartAttack1(); return; }
        if (isAttacking && comboStep == 1 && comboWindowOpen) comboQueued = true;
    }

    private bool CanStartAttack1() => Time.time >= nextAttackAllowedAt;

    private void TryStartAttack1()
    {
        if (!CanStartAttack1()) return;

        // Interrompi eventuale blend-out precedente
        if (blendOutCR != null) { StopCoroutine(blendOutCR); blendOutCR = null; }

        // Pre-raise
        SetAttackLayerWeight(1f);
        SetAttacking(true);

        // Prep
        comboStep = 1;
        comboQueued = false;
        comboWindowOpen = false;
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);

        // Ingresso A1 (anti self-transition)
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
        SetAttackLayerWeight(1f);
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
        if (hitbox) hitbox.SetActive(false);
        if (hitstopper) hitstopper.DoHitstop(hitstopSeconds);
        if (cameraJolt) cameraJolt.DoJolt(cameraJoltSeconds);
    }

    private void OnRecoverStart()
    {
        if (playerMove) playerMove.SetExternalSpeedMultiplier(recoverSpeedMul);

        // Blend-out morbido del layer durante il recover
        var st = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        bool inA2Now = (st.shortNameHash == a2ShortHash);
        float time = inA2Now ? blendOutTimeA2 : blendOutTimeA1;

        if (blendOutCR != null) StopCoroutine(blendOutCR);
        blendOutCR = StartCoroutine(BlendOutRoutine(time, blendOutDelay));

        // Sblocco deterministico (manteniamo la logica esistente)
        if (inA2Now || (comboStep == 1 && !(hasNextComboParam && animator.GetBool(nextComboParam))))
        {
            if (debugLogs) Debug.Log("[Combat] RecoverStart → unlock");
            EndOrChain(false); // false = non forzare weight a 0 qui; ci pensa BlendOutRoutine
        }
    }

    private void OnComboOpen()
    {
        comboWindowOpen = true;
        comboQueued = false;
        comboWindowToken++;
        comboWindowTokenProcessed = -1;
        if (debugLogs) Debug.Log("[Combat] Combo window OPEN");
    }

    private void OnComboClose()
    {
        if (!comboWindowOpen) return;
        comboWindowOpen = false;

        if (comboWindowTokenProcessed == comboWindowToken) return;
        comboWindowTokenProcessed = comboWindowToken;

        if (comboStep == 1 && comboQueued)
        {
            if (hasNextComboParam) animator.SetBool(nextComboParam, true); // A1 → A2
            comboQueued = false;
            if (debugLogs) Debug.Log("[Combat] Combo queued → NextCombo TRUE");
        }
        else if (comboStep == 1 && !(hasNextComboParam && animator.GetBool(nextComboParam)))
        {
            if (debugLogs) Debug.Log("[Combat] Combo window CLOSE (no input) → unlock");
            EndOrChain(false); // lascia il blend-out gestire il weight
        }
    }

    public void AttackClipEnd() => EndOrChain(false);

    // ================= FINE CATENA / SBLOCCO =================
    private void EndOrChain(bool forceWeightZero = true)
    {
        // Se stiamo per entrare in A2, non rilasciare ora
        if (comboStep == 1 && hasNextComboParam && animator.GetBool(nextComboParam)) return;

        // Fine catena (A2) o A1 senza combo → rilascia
        if (comboStep >= 2 || (comboStep == 1 && !(hasNextComboParam && animator.GetBool(nextComboParam))))
        {
            comboStep = 0;
            comboWindowOpen = false;
            comboQueued = false;

            SetAttacking(false);
            if (hasNextComboParam) animator.SetBool(nextComboParam, false);
            if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);

            nextAttackAllowedAt = Time.time + 0.35f;

            // Se non stiamo già sfumando, e serve, abbassa di colpo (fallback sicurezza)
            if (forceWeightZero && blendOutCR == null)
                SetAttackLayerWeight(0f);

            if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

            if (!string.IsNullOrEmpty(attackLayerIdleState))
                animator.Play(attackLayerIdleState, attackLayerIndex, 0f);

            // Force-unlock al frame successivo contro rimbalzi
            StartCoroutine(ForceUnlockNextFrame());

            if (debugLogs) Debug.Log("[Combat] Chain END → unlock & cooldown");
        }
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

    // ================= Blend utilities =================
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

    // ================= Layer weight (fallback generale) =================
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

    // ================= Aim Assist =================
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

    // ================= Lunge =================
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

    // ================= Utils =================
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
