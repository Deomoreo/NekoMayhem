using UnityEngine;
using UnityEngine.InputSystem;

public class CatController : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    private CatInputActions controls;
    private CatCombat combat;

    public float walkSpeed;
    public float runSpeed;
    public float jumpForce;
    public float rotationSpeed;

    private Vector2 moveInput;
    private bool isJumping; // 🔥 Controlla se il player sta saltando

    void Awake()
    {
        controls = new CatInputActions();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        combat = GetComponent<CatCombat>();

        controls.Move.Newaction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Move.Newaction.canceled += ctx => moveInput = Vector2.zero;
        controls.Attack.Newaction.performed += _ => combat.PerformAttack();

        controls.Jump.Newaction.performed += _ => StartJump();
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

    void StartJump()
    {
        if (isJumping) return;

        isJumping = true; // 🔥 Attiviamo il booleano per l'animazione
        animator.SetBool("IsJumping", true);
    }

    // 🔥 Questo metodo verrà chiamato dall'animazione quando il player spinge verso l'alto
    public void JumpStart()
    {
        if (!isJumping) return;

        rb.velocity = new Vector3(rb.velocity.x, jumpForce, rb.velocity.z);
        Debug.Log("🚀 Jump Start: Il player si solleva!");
    }

    // 🔥 Questo metodo verrà chiamato dall'animazione quando il player atterra
    public void JumpEnd()
    {
        isJumping = false;
        animator.SetBool("IsJumping", false);
        Debug.Log("🏁 Jump End: Il player è atterrato!");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            JumpEnd(); // 🔥 Se tocca terra prima della fine dell'animazione, forziamo l'atterraggio
        }
    }
}
