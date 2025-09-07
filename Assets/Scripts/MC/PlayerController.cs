using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCam;

    private CharacterController cc;
    private PlayerInput pi;
    private Animator anim;

    [Header("Movement")]
    [SerializeField, Range(0f, 15f)] private float walkSpeed = 4.0f;
    [SerializeField, Range(0f, 25f)] private float runSpeed = 6.5f;
    [SerializeField, Range(0f, 50f)] private float acceleration = 25f;
    [SerializeField, Range(0f, 50f)] private float deceleration = 30f;
    [SerializeField, Range(0f, 30f)] private float rotationSharpness = 14f;
    [SerializeField] private bool useSprint = false;
    [SerializeField, Range(0f, 0.3f)] private float inputDeadzone = 0.08f;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundStick = -5f;
    [SerializeField] private float skinDownForce = -2f;

    [Header("Dash / Dodge")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.4f;
    [SerializeField] private float iFrameDuration = 0.18f;

    [Header("Combat Windows")]
    [SerializeField] private float attackBuffer = 0.18f;
    [SerializeField] private float parryBuffer = 0.12f;

    [Header("Animator Parameters")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isDashingParam = "IsDashing";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string parryTrigger = "Parry";
    [SerializeField] private string moveXParam = "MoveX"; // per 2D blend
    [SerializeField] private string moveZParam = "MoveZ"; // per 2D blend

    // Runtime
    private Vector2 moveInput;        // letto in Update
    private bool sprintHeld;
    private bool canDash = true;
    private bool isDashing = false;
    private bool invulnerable = false;

    private Vector3 velocity;         // Y (gravità)
    private Vector3 planarVel;        // XZ corrente
    private Vector3 desiredDir;       // XZ target da input
    private Vector3 lastPlanarDir;    // ultima dir valida (per dash/rotazione)

    // Buffer input
    private float lastAttackPressedTime = Mathf.NegativeInfinity;
    private float lastParryPressedTime = Mathf.NegativeInfinity;

    // Nomi azioni
    private const string MAP_NAME = "Gameplay";
    private const string MOVE = "Move";
    private const string DODGE = "Dodge";
    private const string ATTACK = "Attack";
    private const string PARRY = "Parry";
    private const string SPRINT = "Sprint";

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        pi = GetComponent<PlayerInput>();
        anim = GetComponent<Animator>();

        if (!mainCam) mainCam = Camera.main;
    }

    private void OnEnable()
    {
        // Registriamo solo i bottoni; Move verrà letto in polling.
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
        // ===== 0) Lettura input in polling =====
        moveInput = GetAction(MOVE).ReadValue<Vector2>();
        if (moveInput.sqrMagnitude < inputDeadzone * inputDeadzone)
            moveInput = Vector2.zero;

        // ===== 1) Direzione camera-relative =====
        Vector3 camForward = mainCam ? mainCam.transform.forward : Vector3.forward;
        Vector3 camRight = mainCam ? mainCam.transform.right : Vector3.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        desiredDir = (camForward * moveInput.y + camRight * moveInput.x);
        float inputMag = Mathf.Clamp01(desiredDir.magnitude);

        if (inputMag > 0.0001f)
            lastPlanarDir = desiredDir.normalized;

        // ===== 2) Velocità target =====
        float targetSpeed = (useSprint && sprintHeld) ? runSpeed : walkSpeed;
        Vector3 targetPlanarVel = desiredDir.normalized * (targetSpeed * inputMag);

        // ===== 3) Acc/Dec =====
        float sharpness = (targetPlanarVel.magnitude > planarVel.magnitude) ? acceleration : deceleration;
        planarVel = Vector3.MoveTowards(planarVel, targetPlanarVel, sharpness * Time.deltaTime);

        // ===== 4) Rotazione verso direzione di marcia =====
        Vector3 faceDir = (inputMag > 0.0001f) ? desiredDir : lastPlanarDir;
        if (faceDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(faceDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
        }

        // ===== 5) Gravità =====
        if (isDashing)
        {
            // Durante il dash applichiamo solo stick e gravità lieve
            ApplyGravityDuringDash();
        }
        else
        {
            if (cc.isGrounded) velocity.y = skinDownForce;
            else velocity.y += gravity * Time.deltaTime;
        }

        // ===== 6) Movimento finale =====
        Vector3 displacement = (planarVel + velocity) * Time.deltaTime;
        cc.Move(displacement);

        // ===== 7) Animator =====
        UpdateAnimator(targetSpeed > 0f ? planarVel.magnitude / Mathf.Max(targetSpeed, 0.0001f) : 0f);

        // ===== 8) Buffer consumo semplice =====
        TryConsumeBufferedCombat();
    }

    private void ApplyGravityDuringDash()
    {
        velocity.y = cc.isGrounded ? groundStick : velocity.y + gravity * Time.deltaTime;
    }

    private void UpdateAnimator(float normalizedSpeed)
    {
        if (!anim) return;
        anim.SetFloat(speedParam, normalizedSpeed);
        if (!string.IsNullOrEmpty(moveXParam)) anim.SetFloat(moveXParam, moveInput.x);
        if (!string.IsNullOrEmpty(moveZParam)) anim.SetFloat(moveZParam, moveInput.y);
        anim.SetBool(isDashingParam, isDashing);
    }

    // ===== Input (bottoni) =====
    private void OnDodge(InputAction.CallbackContext ctx)
    {
        if (!isDashing && canDash && lastPlanarDir.sqrMagnitude > 0.0001f)
            StartCoroutine(DashRoutine(lastPlanarDir));
    }

    private void OnAttack(InputAction.CallbackContext ctx)
    {
        lastAttackPressedTime = Time.time;
        if (!isDashing)
            anim.SetTrigger(attackTrigger);
    }

    private void OnParry(InputAction.CallbackContext ctx)
    {
        lastParryPressedTime = Time.time;
        if (!isDashing)
            anim.SetTrigger(parryTrigger);
    }

    private void TryConsumeBufferedCombat()
    {
        if (isDashing) return;

        if (Time.time - lastAttackPressedTime <= attackBuffer)
        {
            anim.SetTrigger(attackTrigger);
            lastAttackPressedTime = Mathf.NegativeInfinity;
        }
        if (Time.time - lastParryPressedTime <= parryBuffer)
        {
            anim.SetTrigger(parryTrigger);
            lastParryPressedTime = Mathf.NegativeInfinity;
        }
    }

    // ===== Dash / i-frames =====
    private IEnumerator DashRoutine(Vector3 dir)
    {
        isDashing = true;
        canDash = false;
        invulnerable = true;

        Vector3 dashVel = dir.normalized * dashSpeed;
        float t = 0f;
        float iFrameEnd = iFrameDuration;

        // Blocca planarVel al valore del dash durante la finestra
        while (t < dashDuration)
        {
            planarVel = dashVel;
            t += Time.deltaTime;
            if (t > iFrameEnd) invulnerable = false;
            yield return null;
        }

        isDashing = false;
        invulnerable = false;

        // Rientro morbido alla velocità “walk”
        planarVel = dir.normalized * walkSpeed;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // ===== Animation Events =====
    public void AE_AttackStart() { /* startup: preparazioni */ }

    public void AE_AttackActive()
    {
        // attiva hitbox se c’è
        var hb = GetComponentInChildren<Hitbox>(true);
        if (hb) hb.SetActive(true);
    }

    public void AE_AttackEnd()
    {
        // disattiva hitbox e resetta trigger per sicurezza
        var hb = GetComponentInChildren<Hitbox>(true);
        if (hb) hb.SetActive(false);

        if (anim)
        {
            anim.ResetTrigger(attackTrigger);
            anim.ResetTrigger(parryTrigger); // nel caso di parry finito in questa clip
        }
    }

    // ===== Helpers Input =====
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

    // API
    public bool IsInvulnerable() => invulnerable;
    public Vector3 GetPlanarVelocity() => planarVel;
}
