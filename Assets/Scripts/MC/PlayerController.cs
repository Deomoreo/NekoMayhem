using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCam;
    [Tooltip("Se il tuo Animator è su un child, trascinalo qui. Altrimenti lascia vuoto e useremo GetComponent<Animator>() sullo stesso GO.")]
    [SerializeField] private Animator animatorOverride;

    private CharacterController cc;
    private PlayerInput pi;
    private Animator anim;

    [Header("Movement")]
    [SerializeField, Range(0f, 15f)] private float walkSpeed = 4.0f;
    [SerializeField, Range(0f, 25f)] private float runSpeed = 6.5f;
    [SerializeField, Range(0f, 60f)] private float acceleration = 25f;
    [SerializeField, Range(0f, 80f)] private float deceleration = 45f;
    [SerializeField, Range(0f, 30f)] private float rotationSharpness = 20f;
    [SerializeField] private bool useSprint = false;
    [SerializeField, Range(0f, 0.3f)] private float inputDeadzone = 0.1f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundStick = -5f;
    [SerializeField] private float skinDownForce = -2f;

    [Header("Dash / Dodge")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.4f;
    [SerializeField] private float iFrameDuration = 0.18f;

    [Header("Combat / Buffer")]
    [SerializeField] private float attackBuffer = 0.18f;
    [SerializeField] private float parryBuffer = 0.12f;

    [Header("Anti-Spam / Cadence")]
    [SerializeField] private float minAttackInterval = 0.30f;

    [Header("Combo Settings")]
    [Tooltip("Se TRUE, usa gli Animation Events (AE_...). Se FALSE, usa la finestra temporale su normalizedTime.")]
    [SerializeField] private bool useAnimEvents = false;
    [Tooltip("Inizio finestra combo (Attack1) in normalizedTime)")]
    [SerializeField, Range(0f, 1f)] private float combo1OpenNorm = 0.35f;
    [Tooltip("Fine finestra combo (Attack1) in normalizedTime)")]
    [SerializeField, Range(0f, 1f)] private float combo1CloseNorm = 0.70f;

    [Header("Animator Parameters")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isDashingParam = "IsDashing";
    [SerializeField] private string isAttackingParam = "IsAttacking";
    [SerializeField] private string nextComboParam = "NextCombo";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string parryTrigger = "Parry";
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveZParam = "MoveZ";

    // Runtime movement
    private Vector2 moveInput;
    private bool sprintHeld;
    private Vector3 velocity;
    private Vector3 planarVel;
    private Vector3 desiredDir;
    private Vector3 lastPlanarDir;

    // Runtime combat
    private bool isDashing = false;
    private bool canDash = true;
    private bool invulnerable = false;

    private bool isAttacking = false; // lock
    private int comboStep = 0;        // 0 none, 1 A1, 2 A2
    private bool comboWindowOpen = false;
    private bool comboQueued = false;
    private float nextAttackTime = 0f;

    private float lastAttackPressedTime = Mathf.NegativeInfinity;
    private float lastParryPressedTime = Mathf.NegativeInfinity;

    // Input map & actions
    private const string MAP_NAME = "Gameplay";
    private const string MOVE = "Move";
    private const string DODGE = "Dodge";
    private const string ATTACK = "Attack";
    private const string PARRY = "Parry";
    private const string SPRINT = "Sprint";

    // Debug
    [SerializeField] private bool debugLogs = false;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        pi = GetComponent<PlayerInput>();
        anim = animatorOverride ? animatorOverride : GetComponent<Animator>();
        if (!mainCam) mainCam = Camera.main;
    }

    private void OnEnable()
    {
        GetAction(DODGE).performed += OnDodge;
        GetAction(ATTACK).performed += OnAttack;
        GetAction(PARRY).performed += OnParry;

        if (HasAction(SPRINT))
        {
            GetAction(SPRINT).performed += ctx => sprintHeld = true;
            GetAction(SPRINT).canceled += ctx => sprintHeld = false;
        }
    }

    private void OnDisable()
    {
        if (pi && pi.actions != null)
        {
            GetAction(DODGE).performed -= OnDodge;
            GetAction(ATTACK).performed -= OnAttack;
            GetAction(PARRY).performed -= OnParry;

            if (HasAction(SPRINT))
            {
                GetAction(SPRINT).performed -= ctx => sprintHeld = true;
                GetAction(SPRINT).canceled -= ctx => sprintHeld = false;
            }
        }
    }

    private void Update()
    {
        // --- INPUT POLLING ---
        moveInput = GetAction(MOVE).ReadValue<Vector2>();
        if (moveInput.sqrMagnitude < inputDeadzone * inputDeadzone) moveInput = Vector2.zero;

        // --- CAMERA-RELATIVE DIR ---
        Vector3 camF = mainCam ? mainCam.transform.forward : Vector3.forward;
        Vector3 camR = mainCam ? mainCam.transform.right : Vector3.right;
        camF.y = 0f; camR.y = 0f; camF.Normalize(); camR.Normalize();

        desiredDir = (camF * moveInput.y + camR * moveInput.x);
        float inputMag = Mathf.Clamp01(desiredDir.magnitude);
        if (inputMag > 0.0001f) lastPlanarDir = desiredDir.normalized;

        // --- SPEED TARGET ---
        float targetSpeed = (useSprint && sprintHeld) ? runSpeed : walkSpeed;
        Vector3 targetPlanarVel = desiredDir.normalized * (targetSpeed * inputMag);

        // --- ACC/DEC ---
        float sharp = (targetPlanarVel.magnitude > planarVel.magnitude) ? acceleration : deceleration;
        planarVel = Vector3.MoveTowards(planarVel, targetPlanarVel, sharp * Time.deltaTime);

        // --- ROTATION ---
        Vector3 faceDir = (inputMag > 0.0001f) ? desiredDir : lastPlanarDir;
        if (faceDir.sqrMagnitude > 0.0001f)
        {
            Quaternion tRot = Quaternion.LookRotation(faceDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, tRot, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
        }

        // --- GRAVITY ---
        if (isDashing) ApplyGravityDuringDash();
        else velocity.y = cc.isGrounded ? skinDownForce : velocity.y + gravity * Time.deltaTime;

        // --- MOVE ---
        cc.Move((planarVel + velocity) * Time.deltaTime);

        // --- ANIMATOR ---
        float normalized = planarVel.magnitude / Mathf.Max(runSpeed, 0.0001f);
        UpdateAnimator(Mathf.Clamp01(normalized));

        // --- COMBO SENZA EVENTI (opzionale) ---
        if (!useAnimEvents)
            HandleComboWindowByTime(); // apre/chiude finestra su Attack1 in base a normalizedTime

        // --- FALLBACK LOCK da TAG (se mancano eventi) ---
        var st = anim.GetCurrentAnimatorStateInfo(0);
        bool inAttackTag = st.IsTag("Attack") && st.normalizedTime < 0.98f;
        if (inAttackTag) SetIsAttacking(true);

        // Unstick di sicurezza: se NON siamo più in tag Attack ma il lock è rimasto
        if (!inAttackTag && isAttacking && comboStep == 0)
        {
            if (debugLogs) Debug.Log("Unstick: clear attacking/NextCombo");
            SetIsAttacking(false);
            anim.SetBool(nextComboParam, false);
        }

        // --- BUFFER ---
        TryConsumeBufferedCombat();
    }

    private void ApplyGravityDuringDash()
    {
        velocity.y = cc.isGrounded ? groundStick : velocity.y + gravity * Time.deltaTime;
    }

    private void UpdateAnimator(float speedNorm)
    {
        if (!anim) return;
        anim.SetFloat(speedParam, speedNorm);
        if (!string.IsNullOrEmpty(moveXParam)) anim.SetFloat(moveXParam, moveInput.x);
        if (!string.IsNullOrEmpty(moveZParam)) anim.SetFloat(moveZParam, moveInput.y);
        anim.SetBool(isDashingParam, isDashing);
        anim.SetBool(isAttackingParam, isAttacking);
        // nextComboParam è gestito a eventi o via HandleComboWindowByTime
    }

    // ================= INPUT =================
    private void OnDodge(InputAction.CallbackContext ctx)
    {
        if (!isDashing && canDash && lastPlanarDir.sqrMagnitude > 0.0001f)
            StartCoroutine(DashRoutine(lastPlanarDir));
    }

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        lastAttackPressedTime = Time.time;

        // Se non sto attaccando → avvia subito Attack1
        if (!isAttacking) { TryStartAttack1Immediate(); return; }

        // Se sono in Attack1 e finestra è aperta → queue per Attack2
        if (isAttacking && comboStep == 1 && comboWindowOpen)
            comboQueued = true;
    }

    private void OnParry(InputAction.CallbackContext ctx)
    {
        lastParryPressedTime = Time.time;
        if (isDashing || isAttacking) return;
        anim.SetTrigger(parryTrigger);
    }

    // ================= ATTACK LOGIC =================
    private void TryStartAttack1Immediate()
    {
        if (!CanStartAttack1()) return;

        comboStep = 1;
        comboQueued = false;
        comboWindowOpen = false;

        // Niente lock qui: lasciamo partire la transizione subito
        anim.ResetTrigger(attackTrigger);
        anim.SetBool(nextComboParam, false);
        anim.SetTrigger(attackTrigger);

        if (debugLogs) Debug.Log("Attack1 START (trigger)");
    }

    private bool CanStartAttack1()
    {
        if (isDashing) return false;
        if (isAttacking) return false;
        if (Time.time < nextAttackTime) return false;
        return true;
    }

    private void TryConsumeBufferedCombat()
    {
        // Buffer Attack1
        if (!isDashing && !isAttacking && (Time.time - lastAttackPressedTime) <= attackBuffer)
        {
            TryStartAttack1Immediate();
            lastAttackPressedTime = Mathf.NegativeInfinity;
        }

        // Buffer Parry
        if (!isDashing && !isAttacking && (Time.time - lastParryPressedTime) <= parryBuffer)
        {
            anim.SetTrigger(parryTrigger);
            lastParryPressedTime = Mathf.NegativeInfinity;
        }
    }

    private IEnumerator DashRoutine(Vector3 dir)
    {
        isDashing = true; canDash = false; invulnerable = true;

        Vector3 dashVel = dir.normalized * dashSpeed;
        float t = 0f, iFrameEnd = iFrameDuration;

        while (t < dashDuration)
        {
            planarVel = dashVel;
            t += Time.deltaTime;
            if (t > iFrameEnd) invulnerable = false;
            yield return null;
        }

        isDashing = false; invulnerable = false;
        planarVel = dir.normalized * walkSpeed;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // ================= COMBO SENZA EVENTI =================
    private void HandleComboWindowByTime()
    {
        var st = anim.GetCurrentAnimatorStateInfo(0);

        // Siamo nel primo attacco?
        bool inAttack = st.IsTag("Attack");
        if (!(inAttack && comboStep == 1)) { comboWindowOpen = false; return; }

        float t = st.normalizedTime; // può superare 1 su loop, ma le clip d'attacco sono non in loop
        bool openNow = (t >= combo1OpenNorm && t <= combo1CloseNorm);

        // Edge open
        if (!comboWindowOpen && openNow)
        {
            comboWindowOpen = true;
            comboQueued = false;
            if (debugLogs) Debug.Log("Combo Window OPEN");
        }

        // Edge close
        if (comboWindowOpen && t > combo1CloseNorm)
        {
            comboWindowOpen = false;
            if (debugLogs) Debug.Log("Combo Window CLOSE");

            if (comboQueued)
            {
                // forza la transizione a Attack2
                anim.SetBool(nextComboParam, true);
                comboQueued = false;
                if (debugLogs) Debug.Log("NextCombo TRUE (by time)");
            }
        }
    }

    // ================= ANIMATION EVENTS (se li usi) =================
    public void AE_AttackStart()
    {
        SetIsAttacking(true);
        if (comboStep == 0) comboStep = 1;
        if (debugLogs) Debug.Log("AE_AttackStart");
    }

    public void AE_AttackActive()
    {
        var hb = GetComponentInChildren<Hitbox>(true);
        if (hb) hb.SetActive(true);
        if (debugLogs) Debug.Log("AE_AttackActive (Hitbox ON)");
    }

    public void AE_ComboWindowOpen()
    {
        if (!useAnimEvents) return;
        comboWindowOpen = true;
        comboQueued = false;
        if (debugLogs) Debug.Log("AE_ComboWindowOpen");
    }

    public void AE_ComboWindowClose()
    {
        if (!useAnimEvents) return;
        comboWindowOpen = false;
        if (comboStep == 1 && comboQueued)
        {
            anim.SetBool(nextComboParam, true);
            comboQueued = false;
            if (debugLogs) Debug.Log("AE_ComboWindowClose -> NextCombo TRUE");
        }
    }

    public void AE_AttackEnd()
    {
        var hb = GetComponentInChildren<Hitbox>(true);
        if (hb) hb.SetActive(false);

        anim.ResetTrigger(attackTrigger);
        anim.ResetTrigger(parryTrigger);

        // Se stiamo per passare ad Attack2, mantieni lock
        if (comboStep == 1 && anim.GetBool(nextComboParam))
        {
            if (debugLogs) Debug.Log("AE_AttackEnd (A1) but NextCombo TRUE → keep lock");
            return;
        }

        // Fine catena o A1 senza combo
        if (comboStep >= 2 || (comboStep == 1 && !anim.GetBool(nextComboParam)))
        {
            comboStep = 0;
            SetIsAttacking(false);
            anim.SetBool(nextComboParam, false);
            nextAttackTime = Time.time + minAttackInterval;
            if (debugLogs) Debug.Log("AE_AttackEnd → lock off, cooldown set");
        }
    }

    public void AE_Attack2Start()
    {
        comboStep = 2;
        SetIsAttacking(true);
        anim.SetBool(nextComboParam, false); // reset per sicurezza
        if (debugLogs) Debug.Log("AE_Attack2Start");
    }

    // ================= HELPERS =================
    private void SetIsAttacking(bool value)
    {
        isAttacking = value;
        if (anim) anim.SetBool(isAttackingParam, value);
    }

    private InputAction GetAction(string actionName)
    {
        var map = pi.actions.FindActionMap(MAP_NAME, true);
        return map.FindAction(actionName, true);
    }
    private bool HasAction(string actionName)
    {
        var map = pi.actions.FindActionMap(MAP_NAME, false);
        if (map == null) return false;
        return map.FindAction(actionName, false) != null;
    }

    // API utili
    public bool IsInvulnerable() => invulnerable;
    public Vector3 GetPlanarVelocity() => planarVel;
}
