using UnityEngine;
using UnityEngine.InputSystem;

public class CatController : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    private CatInputActions controls;
    private CatCombat combat; // Riferimento al sistema di attacco

    public float walkSpeed;
    public float runSpeed;
    public float jumpForce;
    public float rotationSpeed;

    private Vector2 moveInput;
    private bool isJumping;

    void Awake()
    {
        controls = new CatInputActions();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        combat = GetComponent<CatCombat>(); // Prende il sistema di attacco

        controls.Move.Newaction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Move.Newaction.canceled += ctx => moveInput = Vector2.zero;

        controls.Attack.Newaction.performed += _ => combat.PerformAttack(); // Chiamata all'attacco

        controls.Jump.Newaction.performed += _ => Jump();
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Update()
    {
        Move();
    }

    void Move()
    {
        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;

        forward.y = 0;
        right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = (right * moveInput.x + forward * moveInput.y).normalized;
        float currentSpeed = moveInput.magnitude > 0 ? (controls.Run.Newaction.IsPressed() ? runSpeed : walkSpeed) : 0f;

        transform.Translate(direction * currentSpeed * Time.deltaTime, Space.World);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
        animator.SetFloat("Speed", currentSpeed);
    }

    void Jump()
    {
        if (isJumping) return;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        isJumping = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isJumping = false;
        }
    }
}
