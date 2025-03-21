using UnityEngine;
using UnityEngine.InputSystem;

public class CatController : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    private CatInputActions controls;
    private CatCombat combat;
    private Camera mainCamera;

    public float walkSpeed;
    private float baseSpeed = 2f;
    public float rotationSpeed;
    public float speedSmoothFactor = 5f;
    public float speedThreshold = 0.01f;

    private bool lockRotation = false;
    private bool parryRotationActive = false;
    private bool canMove = true;

    private Vector2 moveInput;

    private CatDash dash;
    private CatParry parry;

    void Awake()
    {
        controls = new CatInputActions();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        combat = GetComponent<CatCombat>();
        mainCamera = Camera.main;
        dash = GetComponent<CatDash>();
        parry = GetComponent<CatParry>();

        controls.Move.Newaction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Move.Newaction.canceled += ctx => moveInput = Vector2.zero;
        controls.Attack.Newaction.performed += _ =>
        {
            // Se dash o parry sono attivi, non esegue l'attacco
            if ((dash != null && dash.IsDashing) || (parry != null && parry.IsParryingActive))
                return;
            combat.PerformAttack();
        };
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Update()
    {
        Move();
        if (parryRotationActive)
        {
            // Puoi attivare la rotazione verso il cursore se necessario
            // RotateToCursor();
        }
    }
    public void EnableMovement(bool state)
    {
        canMove = state;
    }

    void RotateToCursor()
    {
        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
        {
            Vector3 targetPosition = new Vector3(hit.point.x, transform.position.y, hit.point.z);
            Vector3 direction = (targetPosition - transform.position).normalized;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    public void SetSpeed(float newSpeed)
    {
        walkSpeed = newSpeed;
    }

    public float GetBaseSpeed()
    {
        return baseSpeed;
    }

    public void ModifyMoveSpeed(float multiplier)
    {
        walkSpeed *= multiplier;
    }

    public void LockRotation(bool state)
    {
        lockRotation = state;
    }

    void Move()
    {
        if (!canMove) // <— Aggiunto controllo
            return;

        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;

        forward.y = 0; right.y = 0;
        forward.Normalize(); right.Normalize();

        Vector3 direction = (right * moveInput.x + forward * moveInput.y).normalized;
            
        // Movimento parziale durante attacco (solo rallentato, non bloccato)
        float speedMultiplier = canMove ? 1f : 0.7f; // 70% velocità durante attacco
        float currentSpeed = (moveInput.magnitude > 0 ? walkSpeed : 0f) * speedMultiplier;

        if ((dash != null && dash.IsDashing) || (parry != null && parry.IsParryingActive))
            currentSpeed = 0f;

        transform.Translate(direction * currentSpeed * Time.deltaTime, Space.World);

        if (!lockRotation && direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        UpdateAnimatorSpeed(currentSpeed);
    }



    /// <summary>
    /// Aggiorna gradualmente il parametro "Speed" dell'Animator.
    /// Se il valore interpolato è sufficientemente vicino al target, lo imposta esattamente.
    /// Questo evita che il valore continui ad aggiornarsi in maniera infinita.
    /// </summary>
    /// <param name="targetSpeed">La velocità target (calcolata in base all'input)</param>
    private void UpdateAnimatorSpeed(float targetSpeed)
    {
        if (animator != null)
        {
            float currentAnimSpeed = animator.GetFloat("Speed");
            float smoothSpeed = Mathf.Lerp(currentAnimSpeed, targetSpeed, Time.deltaTime * speedSmoothFactor);
            // Se siamo sufficientemente vicini al target, impostiamo esattamente il target per evitare aggiornamenti infinitesimali
            if (Mathf.Abs(smoothSpeed - targetSpeed) < speedThreshold)
            {
                smoothSpeed = targetSpeed;
            }
            // Clamp per sicurezza, anche se non dovrebbe superare walkSpeed o scendere sotto 0
            smoothSpeed = Mathf.Clamp(smoothSpeed, 0f, walkSpeed);
            animator.SetFloat("Speed", smoothSpeed);
            
        }
    }

    public void EnableParryRotation(bool state)
    {
        parryRotationActive = state;
    }
}
