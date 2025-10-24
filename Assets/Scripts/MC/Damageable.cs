using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Damageable : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float health = 100f;

    [Header("Poise / Stagger")]
    public float maxPoise = 50f;
    public float poise = 50f;
    [Tooltip("Rigenera poise al secondo quando non sei in hitstun/stagger.")]
    public float poiseRegenPerSec = 15f;
    [Tooltip("Tempo dopo aver preso danno prima che riparta la rigenerazione poise.")]
    public float poiseRegenDelay = 1.0f;

    [Header("Hitstun / i-Frames")]
    [Tooltip("Durante gli i-frames non prendi danno.")]
    public bool invulnerable = false;
    public float currentHitstun = 0f;

    [Header("Debug")]
    public bool debugLogs = true;

    public event Action<float> OnHealthChanged;   // float healthNormalized
    public event Action OnStagger;                // chiamato quando poise va a 0
    public event Action OnDeath;
    public event Action<float, Vector3> OnDamaged; // (damage, dir)

    float _lastDamageAt = -999f;

    void Awake()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        poise = Mathf.Clamp(poise, 0f, maxPoise);
    }

    void Update()
    {
        // hitstun countdown
        if (currentHitstun > 0f) currentHitstun -= Time.deltaTime;

        // regen poise
        if (Time.time - _lastDamageAt >= poiseRegenDelay && currentHitstun <= 0f && poise < maxPoise)
        {
            poise = Mathf.Min(maxPoise, poise + poiseRegenPerSec * Time.deltaTime);
        }
    }

    /// <summary>
    /// Applica danno e poise; ritorna true se il danno è stato applicato.
    /// </summary>
    public bool ApplyHit(float damage, float poiseDamage, float hitstunSeconds, Vector3 hitDirection)
    {
        if (invulnerable || health <= 0f) return false;

        _lastDamageAt = Time.time;

        // Poise
        if (poiseDamage > 0f)
        {
            poise = Mathf.Max(0f, poise - poiseDamage);
            if (poise <= 0f)
            {
                OnStagger?.Invoke();
                // reset poise a una quota per evitare chain-lock infiniti
                poise = maxPoise * 0.5f;
            }
        }

        // Danno
        if (damage > 0f)
        {
            health = Mathf.Max(0f, health - damage);
            OnDamaged?.Invoke(damage, hitDirection);
            OnHealthChanged?.Invoke(health / Mathf.Max(1f, maxHealth));
            if (debugLogs) Debug.Log($"[Damageable] {name} - Dmg:{damage} HP:{health:0}/{maxHealth} Poise:{poise:0}");

            if (health <= 0f)
            {
                Die();
                return true;
            }
        }

        // Hitstun
        currentHitstun = Mathf.Max(currentHitstun, hitstunSeconds);
        return true;
    }

    public void SetInvulnerable(float seconds)
    {
        if (seconds <= 0f) return;
        StopAllCoroutines();
        StartCoroutine(CoIFrames(seconds));
    }

    System.Collections.IEnumerator CoIFrames(float sec)
    {
        invulnerable = true;
        yield return new WaitForSeconds(sec);
        invulnerable = false;
    }

    void Die()
    {
        if (debugLogs) Debug.Log($"[Damageable] {name} è morto.");
        OnDeath?.Invoke();
        // placeholder: disattiva oggetto
        gameObject.SetActive(false);
    }
}
