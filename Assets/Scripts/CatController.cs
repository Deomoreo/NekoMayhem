using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CatController : MonoBehaviour
{
    private Animator animator;
    private Rigidbody rb;
    private CatInputActions controls;
    public LayerMask enemyLayers;

    public float walkSpeed;
    public float runSpeed;
    public float jumpForce;
    public float rotationSpeed;
    public float attackAngle = 30f;
    public float attackRange = 1.5f;
    public float attackCooldown = 0.5f;
    public int attackDamage = 10;
    
    private Vector2 moveInput;
    private Vector3 attackOrigin;    
    private Vector3 attackDirection; 
    private bool isJumping;
    public bool isAttacking;


    void Awake()
    {
        controls = new CatInputActions();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        
        controls.Move.Newaction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Move.Newaction.canceled += ctx => moveInput = Vector2.zero;

        controls.Attack.Newaction.performed += _ => StartCoroutine(PerformAttack());

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
        //animator.SetBool("IsJumping", true);
    }
    
    private void ApplyDamage()
    {
        attackOrigin = transform.position;
        attackDirection = transform.gameObject.transform.forward;

        Collider[] hitEnemies = Physics.OverlapSphere(attackOrigin, attackRange, enemyLayers);
       
        foreach (Collider enemy in hitEnemies)
        {
            Vector3 directionToEnemy = (transform.position - attackOrigin).normalized;
            float angle = Vector3.Angle(attackDirection, directionToEnemy);
            if (angle <= attackAngle / 2)
            {
                // Il nemico è davanti al gatto, infliggi danno
                Debug.Log($"Colpito {enemy.name}");
                if (enemy.CompareTag("Enemy"))
                {
                    enemy.GetComponent<EnemyController>().TakeDamage(attackDamage);
                }
            }
        }
    }
    private IEnumerator PerformAttack()
    {
        isAttacking = true;


        animator.SetTrigger("Attack");
        yield return new WaitForSeconds(0.2f); // Sincronizzazione con l'animazione


        yield return new WaitForSeconds(attackCooldown); // Cooldown dell'attacco
        isAttacking = false;
    }
    public void ApplyDamageEvent()
    {
        ApplyDamage();
    }
    void OnDrawGizmos()
    {
            Gizmos.color = Color.red;
            Vector3 forward = transform.forward * attackRange;

            Gizmos.DrawRay(transform.position, Quaternion.Euler(0, attackAngle / 2, 0) * forward);
            Gizmos.DrawRay(transform.position, Quaternion.Euler(0, -attackAngle / 2, 0) * forward);
            Gizmos.DrawWireSphere(transform.position + forward, 0.1f);
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
            //animator.SetBool("IsJumping", false);
        }
    }
}
