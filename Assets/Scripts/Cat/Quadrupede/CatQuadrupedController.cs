using UnityEngine;
using UnityEngine.InputSystem;

public class CatQuadrupedController : MonoBehaviour
{
    private CatInputActions controls;
    private Animator animator;
    private Rigidbody rb;
    private Camera mainCamera;

    [Header("Movement Settings")]
    public float runSpeed = 12f;       // Velocità in forma quadrupede
    public float rotationSpeed = 5f;   // Velocità di rotazione
    private Vector2 moveInput;

    [Header("Jump Settings")]
    public float jumpForce = 10f;         // Forza di salto
    public float groundCheckDistance = 0.7f;  // Distanza per il controllo a terra
    public LayerMask groundLayer;         // Layer per il ground check
    public float jumpCooldown = 0.3f;     // Tempo minimo tra salti
    private float lastJumpTime = -Mathf.Infinity;

    private bool jumpPressed = false;
    private bool isGrounded = false;
    private bool canJump = true;  // Consente il salto solo quando a terra

    void Awake()
    {
        controls = new CatInputActions();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        mainCamera = Camera.main;

        controls.Move.Newaction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Move.Newaction.canceled += ctx => moveInput = Vector2.zero;

        controls.Jump.Newaction.performed += OnJumpPerformed;
        controls.Jump.Newaction.canceled += ctx => jumpPressed = false;
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Update()
    {
        Move();
        CheckGrounded();

        // Salta solo se il tasto è stato premuto, il player è a terra e il cooldown è trascorso
        if (jumpPressed && isGrounded && canJump && Time.time - lastJumpTime >= jumpCooldown)
        {
            Jump();
            lastJumpTime = Time.time;
            canJump = false;  // impedisce ulteriori salti finché non si torna a terra
            jumpPressed = false;
        }
    }

    void Move()
    {
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        forward.y = 0; right.y = 0;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = (right * moveInput.x + forward * moveInput.y).normalized;
        float currentSpeed = moveInput.magnitude > 0 ? runSpeed : 0f;

        transform.Translate(direction * currentSpeed * Time.deltaTime, Space.World);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        if (animator != null)
            animator.SetFloat("Speed", currentSpeed);
    }

    void OnJumpPerformed(InputAction.CallbackContext context)
    {
        jumpPressed = true;
    }

    void CheckGrounded()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
        // Quando si tocca il suolo, abilitiamo il salto e resettiamo il bool dell'animator
        if (isGrounded)
        {
            canJump = true;
            if (animator != null)
                animator.SetBool("IsJumping", false);
        }
    }

    void Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        if (animator != null)
            animator.SetBool("IsJumping", true);
    }
}
