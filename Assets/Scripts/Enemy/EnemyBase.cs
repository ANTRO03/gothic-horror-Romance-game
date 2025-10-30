using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyBase : MonoBehaviour
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
    public bool buffDamage;
    public bool invincibility;
    public bool target;

    [Header("Debuff")]
    public int timeBomb;
    public int bleed;
    public int curse;

    private CombatManager combatManager; // reference to round data & party refs

    protected void Awake()
    {
        // Find CombatManager in the scene
        combatManager = FindFirstObjectByType<CombatManager>();

        int round = 0;
        if (combatManager != null)
            round = combatManager.round;

        // Auto-scale basic HP by round
        maxHealth = 6 + (3 * round);
        currentHealth = maxHealth;

        // Setup health bar
        if (healthBar != null)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }

        // Setup MP bar
        if (mpBar != null)
        {
            mpBar.minValue = 0;
            mpBar.maxValue = maxMP;
            mpBar.value = currentMP;
        }

        Debug.Log($"{name} spawned with {currentHealth}/{maxHealth} HP (Round {round}).");
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
            Debug.Log("Invincibility triggered");
        }

        Debug.Log($"{name} current health: {currentHealth}");
    }

    public virtual void TakeTurn(CombatManager cm)
    {
        if (cm == null)
        {
            Debug.LogWarning($"[{name}] TakeTurn called with null CombatManager.");
            return;
        }

        // Basic damage: scales a bit by round
        int damage = 2 + Mathf.Max(0, cm.round);

        // 1) Guard check: if guard has 'target' flag and is alive, force target + consume flag
        ButlerBase forcedTarget = TryConsumeGuardTarget(cm);
        ButlerBase finalTarget = forcedTarget != null ? forcedTarget : PickRandomAliveButler(cm);

        if (finalTarget == null)
        {
            Debug.Log($"{name} has no valid targets this turn.");
            return;
        }

        try
        {
            finalTarget.TakeDamage(damage);
            Debug.Log($"{name} hits {finalTarget.name} for {damage} (round {cm.round}).");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"{name} failed to damage {finalTarget?.name}: {e.Message}");
        }
    }

    // --- Targeting helpers ---

    private ButlerBase TryConsumeGuardTarget(CombatManager cm)
    {
        var guard = cm.guard;
        if (guard != null && guard.currentHealth > 0 && guard.target)
        {
            guard.target = false; // consume the taunt/mark
            return guard;
        }
        return null;
    }

    private ButlerBase PickRandomAliveButler(CombatManager cm)
    {
        var options = new List<ButlerBase>(3);

        if (cm.guard != null && cm.guard.currentHealth > 0) options.Add(cm.guard);
        if (cm.tailor != null && cm.tailor.currentHealth > 0) options.Add(cm.tailor);
        if (cm.chamberlain != null && cm.chamberlain.currentHealth > 0) options.Add(cm.chamberlain);

        if (options.Count == 0) return null;

        int idx = Random.Range(0, options.Count);
        return options[idx];
    }

    public void BindHealthBar(Slider s)
    {
        healthBar = s;
        if (healthBar != null)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
            healthBar.gameObject.SetActive(true);
        }
    }

    public void ApplyBleedDamage()
    {
        if (bleed <= 0) return;

        int bleedDamage = bleed;
        TakeDamage(bleedDamage);
        Debug.Log($"{name} takes {bleedDamage} bleed damage (Bleed stacks: {bleed}).");

        bleed = Mathf.Max(bleed - 1, 0);
    }

    protected void UpdateHealthUI()
    {
        if (healthBar != null) healthBar.value = currentHealth;
    }

    protected void UpdateMPUI()
    {
        if (mpBar != null) mpBar.value = currentMP;
    }
}
