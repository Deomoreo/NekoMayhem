using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;               // Animator con MC_Animator_v01 (quello che suona A1/A2)
    [SerializeField] private CombatEventsBridge eventsBridge; // sullo stesso GO dell'Animator
    [SerializeField] private CharacterController cc;
    [SerializeField] private PlayerController playerMove;     // movement only
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
    [SerializeField] private float layerLerpDown = 10f;

    [Header("State names (layer Attack)")]
    [SerializeField] private string attack1StateName = "Light_A1";
    [SerializeField] private string attack2StateName = "Light_A2";
    [SerializeField] private string attackLayerIdleState = ""; // opzionale

    [Header("Combo & Buffer")]
    [SerializeField] private float inputBufferSeconds = 0.20f;
    private float lastAttackPressed = -999f;
    private bool isAttacking;
    private int comboStep = 0;               // 0=none, 1=A1, 2=A2
    private bool comboWindowOpen = false;
    private bool comboQueued = false;
    private float nextAttackAllowedAt = 0f;

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

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // Input
    private const string MAP = "Gameplay";
    private const string ATTACK = "Attack";

    // Runtime
    private Coroutine layerCR;
    private int attackTagHash;
    private int a1ShortHash, a2ShortHash;

    // param existence cache
    private bool hasIsAttackingParam, hasNextComboParam, hasAttackTrigger;

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

        // Autodetect Attack2
        if (st.shortNameHash == a2ShortHash && comboStep != 2)
        {
            comboStep = 2;
            if (hasNextComboParam) animator.SetBool(nextComboParam, false);
            SetAttacking(true);
            if (debugLogs) Debug.Log("[Combat] Enter A2 → comboStep=2");
        }

        // Se stiamo attaccando e per sicurezza il weight è sceso, rialzalo
        if (inAttackTag && w < 0.95f) SetAttackLayerWeight(1f);

        // --- FINISH-FIRST: sblocco SOLO a fine clip/stato ---
        // A1 finita, NESSUNA combo → sblocco
        if (comboStep == 1 &&
            st.shortNameHash == a1ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex) &&
            !(hasNextComboParam && animator.GetBool(nextComboParam)))
        {
            if (debugLogs) Debug.Log("[Combat] A1 finished (no combo) → unlock");
            EndChain();
        }

        // A1 finita, combo presente → NON sblocco (lascia che la transizione scatti ora, grazie a Exit Time ON)
        // A2 finita → sblocco
        if (comboStep == 2 &&
            st.shortNameHash == a2ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            if (debugLogs) Debug.Log("[Combat] A2 finished → unlock");
            EndChain();
        }

        // Se non attacchi e il weight è rimasto su, riportalo giù piano
        if (!isAttacking && w > 0.01f) SetAttackLayerWeight(0f);
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

        // Pre-raise
        SetAttackLayerWeight(1f);
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
        // NON sblocchiamo qui: lasciamo finire la clip (finish-first)
    }

    private void OnComboOpen()
    {
        comboWindowOpen = true;
        comboQueued = false;
        if (debugLogs) Debug.Log("[Combat] Combo window OPEN");
    }

    private void OnComboClose()
    {
        comboWindowOpen = false;

        // Se hai premuto in finestra: abilita la combo ma NON transizionare prima della fine (ci pensa l'Exit Time)
        if (comboStep == 1 && comboQueued)
        {
            if (hasNextComboParam) animator.SetBool(nextComboParam, true);
            comboQueued = false;
            if (debugLogs) Debug.Log("[Combat] Combo queued → NextCombo TRUE (wait end)");
        }
        // Se non hai premuto, NON sblocchiamo qui: lo faremo a fine clip
    }

    public void AttackClipEnd() { /* opzionale: non usato in finish-first */ }

    // ================= FINE CATENA =================
    private void EndChain()
    {
        comboStep = 0;
        SetAttacking(false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);

        nextAttackAllowedAt = Time.time + 0.35f;
        SetAttackLayerWeight(0f);
        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

        if (!string.IsNullOrEmpty(attackLayerIdleState))
            animator.Play(attackLayerIdleState, attackLayerIndex, 0f);

        if (debugLogs) Debug.Log("[Combat] Chain END → unlock & cooldown");
    }

    // ================= Layer weight =================
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
