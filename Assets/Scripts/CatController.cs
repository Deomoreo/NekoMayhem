using UnityEngine;
using UnityEngine.InputSystem;

public class CatController : MonoBehaviour
{
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpForce = 5f;

    private Animator animator;
    private Rigidbody rb;
    private CatInput controls;
    private Vector2 moveInput;
    private bool isJumping;
    private bool isAttacking;

    void Awake()
    {
        controls = new CatInput();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        
        
        // Bind inputs
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
        float targetSpeed = moveInput.magnitude > 0 ? (Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed) : 0f;

        // Movimento e animazione
        Vector3 direction = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        transform.Translate(direction * targetSpeed * Time.deltaTime, Space.World);

        // Aggiorna l'animatore
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
            // Logica di attacco
            Debug.Log("Attacco eseguito!");
        }
    }

    void ActivateFuryMode()
    {
        animator.SetBool("IsFuryMode", true);
        // Logica di modalità furia qui
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
