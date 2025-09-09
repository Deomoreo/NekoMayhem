using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Hitbox : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private string targetTag = "Enemy";

    private Collider col;
    private readonly HashSet<Collider> _hitThisSwing = new HashSet<Collider>();

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;
        col.enabled = false;
    }

    public void BeginSwing() => _hitThisSwing.Clear();

    public void SetActive(bool active)
    {
        col.enabled = active;
        if (!active) _hitThisSwing.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!col.enabled) return;
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag)) return;
        if (_hitThisSwing.Contains(other)) return;

        _hitThisSwing.Add(other);

        // TODO: collegare health nemico
        // other.GetComponent<EnemyHealth>()?.TakeDamage(damage);
    }
}
