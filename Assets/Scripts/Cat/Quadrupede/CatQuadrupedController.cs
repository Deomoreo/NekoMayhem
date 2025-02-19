using UnityEngine;
using UnityEngine.InputSystem;

public class CatQuadrupedController : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    private CatInputActions controls;
    private CatCombat combat;
    private Camera mainCamera;

    public float walkSpeed;         
    private float baseSpeed = 4f;   
    public float rotationSpeed;     

    private bool lockRotation = false;
    private bool parryRotationActive = false;
    private Vector2 moveInput;

    void Awake()
    {
        controls = new CatInputActions();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        combat = GetComponent<CatCombat>();
        mainCamera = Camera.main;

        controls.Move.Newaction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Move.Newaction.canceled += ctx => moveInput = Vector2.zero;
        controls.Attack.Newaction.performed += _ =>
        {
            combat.PerformAttack();
        };
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Update()
    {
        Move();
    }

    void Move()
    {
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = (right * moveInput.x + forward * moveInput.y).normalized;
        float currentSpeed = moveInput.magnitude > 0 ? walkSpeed : 0f;

        transform.Translate(direction * currentSpeed * Time.deltaTime, Space.World);
        if (!lockRotation && direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        animator.SetFloat("Speed", currentSpeed);
    }

    public void SetSpeed(float newSpeed) => walkSpeed = newSpeed;
    public float GetBaseSpeed() => baseSpeed;
    public void ModifyMoveSpeed(float multiplier) => walkSpeed *= multiplier;
    public void LockRotation(bool state) => lockRotation = state;
    public void EnableParryRotation(bool state) => parryRotationActive = state;
}
