using UnityEngine;
using UnityEngine.InputSystem;

public class CatController : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpForce = 5f;

    private Animator animator;
    private Rigidbody rb;
    private CatInputActions controls;
    private Vector2 moveInput;
    private bool isJumping;
    private bool isAttacking;

    void Awake()
    {
        controls = new CatInputActions();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        
        controls.Move.Newaction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Move.Newaction.canceled += ctx => moveInput = Vector2.zero;

        controls.Attack.Newaction.performed += _ => Attack();

        controls.Jump.Newaction.performed += _ => Jump();

        controls.FuryMode.Newaction.performed += _ => ActivateFuryMode();
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
        float targetSpeed = moveInput.magnitude > 0 ? (moveInput.y > 0.5f ? runSpeed : walkSpeed) : 0f;

        transform.Translate(direction * targetSpeed * Time.deltaTime, Space.World);

        animator.SetFloat("Speed", targetSpeed);
    }

    void Jump()
    {
        if (isJumping) return;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        isJumping = true;
        animator.SetBool("IsJumping", true);
    }
    private void Attack()
    {
        if (!isAttacking)
        {
            isAttacking = true;
            Debug.Log("Attacco eseguito!");
        }
    }

    void ActivateFuryMode()
    {
        animator.SetBool("IsFuryMode", true);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isJumping = false;
            animator.SetBool("IsJumping", false);
        }
    }
}
