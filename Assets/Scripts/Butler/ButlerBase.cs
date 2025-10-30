using UnityEngine;
using UnityEngine.UI;

public class ButlerBase : MonoBehaviour
{

    [Header("Health")]
    public int maxHealth;
    public int currentHealth;
    [SerializeField] private Slider healthBar;

    [Header("MP")]
    public int maxMP;
    public int currentMP;
    [SerializeField] private Slider mpBar;

    [Header("Buffs")]
    public bool strength;
    public bool invincibility;
    public bool target;

    [Header("Debuff")]
    public int timeBomb;
    public int bleed;
    public int curse;

    protected void Awake()
    {
        currentHealth = maxHealth;

        if (healthBar != null)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }

        if (mpBar != null)
        {
            mpBar.minValue = 0;
            mpBar.maxValue = maxMP;
            mpBar.value = currentMP;
        }
    }

    public void TakeDamage(int damageValue)
    {
        if (!invincibility)
        {
            currentHealth = Mathf.Max(0, currentHealth - damageValue);
            UpdateHealthUI();
        }
        else
        {
            invincibility = false;
            Debug.Log($"{name}: Invincibility triggered, ignored damage.");
        }

        Debug.Log($"{name} current health: {currentHealth}");
    }

    // ========================
    // New Utility Functions
    // ========================

    /// <summary>
    /// Heals the Butler by a given amount, without exceeding maxHealth.
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        int prev = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        UpdateHealthUI();
        Debug.Log($"{name} healed for {currentHealth - prev} (now {currentHealth}/{maxHealth}).");
    }

    // ---- Strength ----
    public void SetStrengthTrue()
    {
        strength = true;
        Debug.Log($"{name} Strength buff ENABLED.");
    }

    public void SetStrengthFalse()
    {
        strength = false;
        Debug.Log($"{name} Strength buff DISABLED.");
    }

    // ---- Invincibility ----
    public void SetInvincibilityTrue()
    {
        invincibility = true;
        Debug.Log($"{name} Invincibility ENABLED.");
    }

    public void SetInvincibilityFalse()
    {
        invincibility = false;
        Debug.Log($"{name} Invincibility DISABLED.");
    }

    // ---- Target ----
    public void SetTargetTrue()
    {
        target = true;
        Debug.Log($"{name} Target flag ENABLED.");
    }

    public void SetTargetFalse()
    {
        target = false;
        Debug.Log($"{name} Target flag DISABLED.");
    }

    // ========================
    // Internal UI Functions
    // ========================

    protected void UpdateHealthUI()
    {
        if (healthBar != null)
            healthBar.value = currentHealth;
    }

    protected void UpdateMPUI()
    {
        if (mpBar != null)
            mpBar.value = currentMP;
    }


}
