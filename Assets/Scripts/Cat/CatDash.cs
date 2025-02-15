using System.Collections;
using UnityEngine;

public class CatDash : MonoBehaviour
{
    public float dashSpeed = 12f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private bool canDash = true;
    private bool isDashing = false;
    private Rigidbody rb;
    private Animator animator;
    private CatInputActions controls;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        controls = new CatInputActions();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    private void Start()
    {
        controls.Dash.Newaction.performed += _ => StartCoroutine(Dash());
    }

    private IEnumerator Dash()
    {
        if (!canDash) yield break;

        canDash = false;
        isDashing = true;
        animator.SetTrigger("Dash");

        Vector3 dashDirection = transform.forward;
        rb.velocity = dashDirection * dashSpeed;
        Physics.IgnoreLayerCollision(11, 8, true);
        yield return new WaitForSeconds(dashDuration);

        isDashing = false;
        rb.velocity = Vector3.zero;
        Physics.IgnoreLayerCollision(11, 8, false);

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // Se durante il dash colpisci un proiettile, lo annulli e applichi un bonus temporaneo
    private void OnTriggerEnter(Collider other)
    {
        if (isDashing && other.CompareTag("Projectile"))
        {
            Destroy(other.gameObject);
            StartCoroutine(ApplyDashBonus());
        }
    }

    private IEnumerator ApplyDashBonus()
    {
        Debug.Log("Dash Parry! Velocità +10% per X secondi!");
        PlayerStats.Instance.ApplyStatUpgrade("Velocità Attacco", 10);
        yield return new WaitForSeconds(6f);
        PlayerStats.Instance.ApplyStatUpgrade("Velocità Attacco", -10);
    }
}
