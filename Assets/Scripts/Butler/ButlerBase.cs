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
    public int shield;
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
        if (shield > 0)
        {
            shield -= 1;
            Debug.Log($"{name}: Shield absorbed the attack. Remaining shield: {shield}");
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damageValue);
        UpdateHealthUI();
        Debug.Log($"{name} took {damageValue} damage (now {currentHealth}/{maxHealth}).");

        if (currentHealth <= 0)
        {
            Debug.Log($"{name} has been defeated.");
        }
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

    public void GainMP(int amount)
    {
        if (amount <= 0) return;
        int before = currentMP;
        currentMP = Mathf.Min(maxMP, currentMP + amount);
        // if you want to block MP gain while KO’d, uncomment:
        // if (currentHealth <= 0) return;

        // make sure the UI reflects the change
        UpdateMPUI();

        // optional: debug
        // Debug.Log($"{name} gained {currentMP - before} MP ({currentMP}/{maxMP}).");
    }

    public void AddShield(int amount)
    {
        if (amount <= 0) return;
        shield += amount;
        Debug.Log($"{name} gained {amount} shield. Total shield: {shield}");
    }

    public void ClearShield()
    {
        shield = 0;
        Debug.Log($"{name}'s shield removed.");
    }


}
