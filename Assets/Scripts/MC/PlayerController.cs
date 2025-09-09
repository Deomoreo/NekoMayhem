using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera mainCam;
    [Tooltip("(Opzionale) L'Animator da aggiornare con Speed/MoveX/MoveZ. Lascia vuoto se non ti serve.")]
    [SerializeField] private Animator anim;

    private CharacterController cc;
    private PlayerInput pi;

    [Header("Movement")]
    [SerializeField, Range(0f, 15f)] private float walkSpeed = 4.0f;
    [SerializeField, Range(0f, 25f)] private float runSpeed = 6.5f;
    [SerializeField, Range(0f, 60f)] private float acceleration = 28f;
    [SerializeField, Range(0f, 80f)] private float deceleration = 52f;
    [SerializeField, Range(0f, 30f)] private float rotationSharpness = 20f;
    [SerializeField, Range(0f, 0.3f)] private float inputDeadzone = 0.1f;
    [SerializeField] private bool useSprint = false;

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundStick = -5f;
    [SerializeField] private float skinDownForce = -2f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.4f;
    [SerializeField] private float iFrameDuration = 0.18f;

    [Header("Animator Params (se anim presente)")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isDashingParam = "IsDashing";
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveZParam = "MoveZ";

    private const string MAP = "Gameplay";
    private const string MOVE = "Move";
    private const string DODGE = "Dodge";
    private const string SPRINT = "Sprint";

    private Vector2 moveInput;
    private bool sprintHeld;
    private bool canDash = true;
    private bool isDashing = false;
    private bool invulnerable = false;

    private Vector3 velocity;
    private Vector3 planarVel;
    private Vector3 desiredDir;
    private Vector3 lastPlanarDir;

    private float externalSpeedMul = 1f;
    private bool rotationOverride = false;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        pi = GetComponent<PlayerInput>();
        if (!mainCam) mainCam = Camera.main;
        // anim è opzionale: se non assegnato, semplicemente non aggiorniamo i parametri
    }

    private void OnEnable()
    {
        var map = pi.actions.FindActionMap(MAP, true);
        map.FindAction(DODGE, true).performed += OnDodge;
        if (HasAction(SPRINT))
        {
            map.FindAction(SPRINT, true).performed += ctx => sprintHeld = true;
            map.FindAction(SPRINT, true).canceled += ctx => sprintHeld = false;
        }
    }

    private void OnDisable()
    {
        var map = pi.actions.FindActionMap(MAP, false);
        if (map != null)
        {
            map.FindAction(DODGE, false).performed -= OnDodge;
            if (HasAction(SPRINT))
            {
                map.FindAction(SPRINT, false).performed -= ctx => sprintHeld = true;
                map.FindAction(SPRINT, false).canceled -= ctx => sprintHeld = false;
            }
        }
    }

    private void Update()
    {
        // Input
        moveInput = pi.actions.FindActionMap(MAP, true).FindAction(MOVE, true).ReadValue<Vector2>();
        if (moveInput.sqrMagnitude < inputDeadzone * inputDeadzone) moveInput = Vector2.zero;

        // Camera-relative
        Vector3 camF = mainCam ? mainCam.transform.forward : Vector3.forward;
        Vector3 camR = mainCam ? mainCam.transform.right : Vector3.right;
        camF.y = 0f; camR.y = 0f; camF.Normalize(); camR.Normalize();

        desiredDir = (camF * moveInput.y + camR * moveInput.x);
        float inputMag = Mathf.Clamp01(desiredDir.magnitude);
        if (inputMag > 0.0001f) lastPlanarDir = desiredDir.normalized;

        // Velocità target
        float baseSpeed = (useSprint && sprintHeld) ? runSpeed : walkSpeed;
        float targetSpeed = baseSpeed * Mathf.Clamp(externalSpeedMul, 0f, 1.5f);
        Vector3 targetPlanarVel = desiredDir.normalized * (targetSpeed * inputMag);

        // Acc/Dec
        float sharp = (targetPlanarVel.magnitude > planarVel.magnitude) ? acceleration : deceleration;
        planarVel = Vector3.MoveTowards(planarVel, targetPlanarVel, sharp * Time.deltaTime);

        // Rotazione
        if (!rotationOverride)
        {
            Vector3 faceDir = (inputMag > 0.0001f) ? desiredDir : lastPlanarDir;
            if (faceDir.sqrMagnitude > 0.0001f)
            {
                Quaternion tRot = Quaternion.LookRotation(faceDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, tRot, 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }
        }

        // Gravità
        velocity.y = cc.isGrounded ? (isDashing ? groundStick : skinDownForce) : velocity.y + gravity * Time.deltaTime;

        // Move
        cc.Move((planarVel + velocity) * Time.deltaTime);

        // Animator (se presente)
        if (anim)
        {
            float normalized = planarVel.magnitude / Mathf.Max(runSpeed, 0.0001f);
            anim.SetFloat(speedParam, Mathf.Clamp01(normalized));
            if (!string.IsNullOrEmpty(moveXParam)) anim.SetFloat(moveXParam, moveInput.x);
            if (!string.IsNullOrEmpty(moveZParam)) anim.SetFloat(moveZParam, moveInput.y);
            anim.SetBool(isDashingParam, isDashing);
        }
    }

    private void OnDodge(InputAction.CallbackContext ctx)
    {
        if (!isDashing && canDash && lastPlanarDir.sqrMagnitude > 0.0001f)
            StartCoroutine(DashRoutine(lastPlanarDir));
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

    // Hooks per il Combat
    public void SetExternalSpeedMultiplier(float mul) => externalSpeedMul = Mathf.Max(0f, mul);
    public void SetRotationOverride(bool enabled) => rotationOverride = enabled;

    private bool HasAction(string name)
    {
        var map = pi.actions.FindActionMap(MAP, false);
        return map != null && map.FindAction(name, false) != null;
    }

    public Vector3 GetPlanarVelocity() => planarVel;
    public bool IsInvulnerable() => invulnerable;
}
