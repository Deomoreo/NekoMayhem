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
    [SerializeField] private string attackLayerIdleState = ""; // opzionale

    // Niente cooldown forzato dopo la fine catena
    private float nextAttackAllowedAt = 0f;

    // Stato combo
    private bool isAttacking;
    private int comboStep = 0;         // 0=none, 1=A1, 2=A2
    private bool comboWindowOpen = false;
    private bool comboQueued = false;

    [Header("Early-Restart A2")]
    [SerializeField, Tooltip("Soglia (normalized time) oltre la quale, se premi durante A2, riparte subito A1.")]
    private float a2RestartThresholdNormalized = 0.96f;

    [Header("Aim/Lunge/FX")]
    [SerializeField] private float aimAssistMaxAngle = 15f;
    [SerializeField] private float aimAssistRange = 2.5f;
    [SerializeField] private float aimLockDuration = 0.10f;
    [SerializeField] private LayerMask enemyMask = ~0;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float windupSpeedMul = 0.55f;
    [SerializeField] private float hitSpeedMul = 0.80f;
    [SerializeField] private float recoverSpeedMul = 1.0f;
    [SerializeField] private float lungeDistance = 1.1f;
    [SerializeField] private float lungeDuration = 0.085f;
    [SerializeField] private float hitstopSeconds = 0.05f;
    [SerializeField] private float cameraJoltSeconds = 0.02f;

    [Header("Blend-out morbido")]
    [SerializeField] private float blendOutDelay = 0.03f;
    [SerializeField] private float blendOutTimeA1 = 0.18f;
    [SerializeField] private float blendOutTimeA2 = 0.24f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // Runtime
    private Coroutine layerCR, blendOutCR;
    private int attackTagHash, a1ShortHash, a2ShortHash;
    private bool hasIsAttackingParam, hasNextComboParam, hasAttackTrigger;

    // Input map/action
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

        // Autodetect A2
        if (st.shortNameHash == a2ShortHash && comboStep != 2)
        {
            comboStep = 2;
            if (hasNextComboParam) animator.SetBool(nextComboParam, false);
            nextComboSetAt = -999f;
            SetAttacking(true);
            StopBlendOut();
            if (debugLogs) Debug.Log("[Combat] Enter A2 → comboStep=2");
        }

        // Se sto in Attack ma il weight è sceso, rialzalo
        if (inAttackTag && w < 0.95f) SetAttackLayerWeight(1f);

        // FINE A1 → decide combo SOLO se è stata premuta una seconda volta nella finestra
        if (comboStep == 1 &&
            st.shortNameHash == a1ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            if (comboQueued || (hasNextComboParam && animator.GetBool(nextComboParam)))
            {
                ForceA2();
            }
            else
            {
                if (debugLogs) Debug.Log("[Combat] A1 finished (no combo) → end");
                EndChainSmooth(false);
            }
        }

        // FINE A2 → end
        if (comboStep == 2 &&
            st.shortNameHash == a2ShortHash &&
            st.normalizedTime >= 0.98f &&
            !animator.IsInTransition(attackLayerIndex))
        {
            if (debugLogs) Debug.Log("[Combat] A2 finished → end");
            EndChainSmooth(true);
        }

        // Watchdog: NextCombo non deve appendersi
        if (hasNextComboParam && animator.GetBool(nextComboParam))
        {
            if (nextComboSetAt < 0f) nextComboSetAt = Time.time;

            bool inA1orA2 = (st.shortNameHash == a1ShortHash) || (st.shortNameHash == a2ShortHash);
            if (!inA1orA2 && !animator.IsInTransition(attackLayerIndex))
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

        // Se non attacco e weight su → abbassa
        if (!isAttacking && w > 0.01f) SetAttackLayerWeight(0f);

        // Failsafe: se non sei in tag Attack e weight basso → spegni bool
        if (isAttacking && !inAttackTag && w <= 0.01f)
            ForceClearAttacking();
    }

    // =============== INPUT ===============
    private void OnAttackPressed(InputAction.CallbackContext ctx)
    {
        // 1) Early-restart SOLO per fine A2: se sei oltre soglia, fai ripartire A1 subito
        if (IsAtEndOfA2())
        {
            // non serve aspettare che isAttacking diventi false: crossfade diretto
            TryStartAttack1();
            return;
        }

        // 2) Primo click → avvia A1 (se non in attacco)
        if (!isAttacking)
        {
            TryStartAttack1();
            return;
        }

        // 3) Secondo click valido SOLO dentro la finestra combo di A1
        if (isAttacking && comboStep == 1 && comboWindowOpen)
        {
            comboQueued = true;
            if (hasNextComboParam) animator.SetBool(nextComboParam, true);
            nextComboSetAt = Time.time;
            if (debugLogs) Debug.Log("[Combat] Combo QUEUED (press in window)");
        }
        // Fuori finestra → ignoriamo (niente buffer, niente hold)
    }

    private bool CanStartAttack1() => Time.time >= nextAttackAllowedAt;

    private void TryStartAttack1()
    {
        if (!CanStartAttack1()) return;

        StopBlendOut();

        SetAttackLayerWeight(1f);
        SetAttacking(true);

        comboStep = 1;
        comboQueued = false;
        comboWindowOpen = false;
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

        // Se NON c’è combo in arrivo → avvia blend-out e chiudi
        bool comboIncoming = (comboStep == 1) && (comboQueued || (hasNextComboParam && animator.GetBool(nextComboParam)));
        if (!comboIncoming)
        {
            float t = (comboStep == 2) ? blendOutTimeA2 : blendOutTimeA1;
            StartBlendOut(t, blendOutDelay);

            if (comboStep == 2 || comboStep == 1)
                EndChain(false); // chiusura deterministica del colpo
        }
        else
        {
            // Combo in arrivo: nessun blend-out, tieni peso alto
            StopBlendOut();
            SetAttackLayerWeight(1f);
            if (debugLogs) Debug.Log("[Combat] RecoverStart (combo incoming) → keep layer UP");
        }
    }

    private void OnComboOpen()
    {
        comboWindowOpen = true;
        if (debugLogs) Debug.Log("[Combat] Combo window OPEN");
    }

    private void OnComboClose()
    {
        comboWindowOpen = false;
        if (debugLogs) Debug.Log("[Combat] Combo window CLOSE");
        // Decisione finale a fine A1 (Update a 0.98)
    }

    public void AttackClipEnd() { /* decisione a 0.98 in Update() */ }

    // =============== COMBO / TRANSIZIONI ===============
    private void ForceA2()
    {
        comboQueued = false;
        StopBlendOut();
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        nextComboSetAt = -999f;

        comboStep = 2;
        SetAttackLayerWeight(1f);
        SetAttacking(true);
        animator.CrossFadeInFixedTime(attack2StateName, 0.05f, attackLayerIndex, 0f);

        if (debugLogs) Debug.Log("[Combat] FORCE A2");
    }

    // =============== FINE CATENA ===============
    private void EndChainSmooth(bool fromA2)
    {
        comboStep = 0;
        comboQueued = false;
        comboWindowOpen = false;

        SetAttacking(false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);
        nextComboSetAt = -999f;

        // Sblocco immediato del prossimo attacco
        nextAttackAllowedAt = Time.time;

        if (playerMove) playerMove.SetExternalSpeedMultiplier(1f);

        StartBlendOut(fromA2 ? blendOutTimeA2 : blendOutTimeA1, blendOutDelay);

        if (!string.IsNullOrEmpty(attackLayerIdleState))
            animator.Play(attackLayerIdleState, attackLayerIndex, 0f);

        StartCoroutine(ForceUnlockNextFrame());
        if (debugLogs) Debug.Log("[Combat] Chain END → ready immediately");
    }

    private void EndChain(bool forceWeightZero = true)
    {
        comboStep = 0;
        comboQueued = false;
        comboWindowOpen = false;

        SetAttacking(false);
        if (hasNextComboParam) animator.SetBool(nextComboParam, false);
        if (hasAttackTrigger) animator.ResetTrigger(attackTrigger);
        nextComboSetAt = -999f;

        // Sblocco immediato del prossimo attacco
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
        comboQueued = false;
        comboWindowOpen = false;
        nextComboSetAt = -999f;
        ForceClearAttacking();
        nextAttackAllowedAt = Time.time; // sempre sbloccato dopo hard reset
    }

    // =============== Blend utils ===============
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

    private bool IsAtEndOfA2()
    {
        var st = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        if (comboStep != 2) return false;
        if (st.shortNameHash != a2ShortHash) return false;
        if (animator.IsInTransition(attackLayerIndex)) return false;
        return st.normalizedTime >= a2RestartThresholdNormalized;
    }

    // =============== Aim / Lunge ===============
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
