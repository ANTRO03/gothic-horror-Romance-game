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
    public bool target;   // if you also mark enemies sometimes

    [Header("Debuff")]
    public int timeBomb;
    public int bleed;
    public int curse;     // REDUCES this enemy's outgoing damage by its value

    private CombatManager combatManager; // reference to round data & party refs

    protected void Awake()
    {
        // Find CombatManager in the scene
        combatManager = FindFirstObjectByType<CombatManager>();

        int round = 0;
        if (combatManager != null)
            round = combatManager.round;

        // Auto-scale basic HP by round (same pattern you had)
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

    // --- Damage in (enemy taking damage) ---
    public void TakeDamage(int damageValue)
    {
        if (!invincibility)
        {
            currentHealth = Mathf.Max(0, currentHealth - damageValue);
            UpdateHealthUI();
        }
        else
        {
            invincibility = false; // one-hit ignore pattern
            Debug.Log($"{name}: Invincibility triggered; damage ignored once.");
        }

        Debug.Log($"{name} current health: {currentHealth}");
    }

    // --- Debuff ticks (call from TurnManager at start of enemy turn) ---
    public void ApplyBleedDamage()
    {
        if (bleed <= 0) return;

        int bleedDamage = bleed;          // deal equal to stacks
        TakeDamage(bleedDamage);
        Debug.Log($"{name} takes {bleedDamage} bleed damage (Bleed stacks: {bleed}).");

        bleed = Mathf.Max(bleed - 1, 0);  // decay by 1
    }

    public void ApplyCurseDamage()
    {
        if (curse <= 0) return;

        int curseDamage = curse;          // deal equal to stacks
        TakeDamage(curseDamage);
        Debug.Log($"{name} takes {curseDamage} curse damage (Curse stacks: {curse}).");

        curse = Mathf.Max(curse - 1, 0);  // decay by 1
    }

    public void IncreaseCurse(int amount)
    {
        if (amount <= 0) return;
        int before = curse;
        curse += amount;
        Debug.Log($"{name} curse increased by {amount} (from {before} to {curse}).");
    }

    // --- Enemy AI action (enemy dealing damage to butlers) ---
    public virtual void TakeTurn(CombatManager cm)
    {
        if (cm == null)
        {
            Debug.LogWarning($"[{name}] TakeTurn called with null CombatManager.");
            return;
        }

        // Base damage: scales a bit by round (keep your original formula)
        int baseDamage = 2 + Mathf.Max(0, cm.round);

        // OUTGOING damage is reduced by THIS ENEMY'S curse stacks (floored at 0)
        int effectiveDamage = Mathf.Max(0, baseDamage - curse);

        // 1) Prefer any marked butler (target == true), consume flag when used
        ButlerBase forcedTarget = TryConsumeMarkedButler(cm);

        // 2) Otherwise pick any random alive butler
        ButlerBase finalTarget = forcedTarget != null ? forcedTarget : PickRandomAliveButler(cm);

        if (finalTarget == null)
        {
            Debug.Log($"{name} has no valid targets this turn.");
            return;
        }

        try
        {
            finalTarget.TakeDamage(effectiveDamage);
            Debug.Log($"{name} hits {finalTarget.name} for {effectiveDamage} (base {baseDamage}, -curse {curse}, round {cm.round}).");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"{name} failed to damage {finalTarget?.name}: {e.Message}");
        }
    }

    // --- Targeting helpers ---

    // Prefer/consume a marked butler (order here defines priority among multiple "target == true")
    private ButlerBase TryConsumeMarkedButler(CombatManager cm)
    {
        var candidates = new List<ButlerBase>(3);

        if (cm.guard != null && cm.guard.currentHealth > 0 && cm.guard.target) candidates.Add(cm.guard);
        if (cm.tailor != null && cm.tailor.currentHealth > 0 && cm.tailor.target) candidates.Add(cm.tailor);
        if (cm.chamberlain != null && cm.chamberlain.currentHealth > 0 && cm.chamberlain.target) candidates.Add(cm.chamberlain);

        if (candidates.Count == 0) return null;

        // choose first by priority (Guard -> Tailor -> Chamberlain). Change order above if you want different priority.
        var chosen = candidates[0];
        chosen.target = false; // consume the taunt/mark immediately after choosing
        return chosen;
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

    // --- UI binders ---

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

    protected void UpdateHealthUI()
    {
        if (healthBar != null) healthBar.value = currentHealth;
    }

    protected void UpdateMPUI()
    {
        if (mpBar != null) mpBar.value = currentMP;
    }
}

