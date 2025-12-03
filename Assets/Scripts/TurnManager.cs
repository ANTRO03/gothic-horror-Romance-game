using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public enum TurnState { PlayerTurn, EnemyTurn }

public class TurnManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CardManager cardManager;        // assign in Inspector
    [SerializeField] private CombatManager combatManager;    // assign in Inspector

    // >>> Use CombatMenuManager for all canvas toggles <<<
    [SerializeField] private CombatMenuManager menuManager;  // assign in Inspector

    // UI Canvases
    [SerializeField] private Canvas cardUICanvas;            // Card picker UI
    [SerializeField] private Canvas statusScreenCanvas;      // Status shell
    [SerializeField] private Canvas allyHealthCanvas;        // Ally HP bars
    [SerializeField] private Canvas enemyHealthCanvas;       // Enemy HP bars

    [Header("Turn Config")]
    [SerializeField] private int cardsPerTurn = 5;
    [SerializeField] private float enemyActDelay = 0.25f;

    [Header("Events (optional for UI)")]
    public UnityEvent OnPlayerTurnStarted;
    public UnityEvent OnEnemyTurnStarted;
    public UnityEvent OnTurnAdvanced;

    public TurnState State { get; private set; }
    public int TurnNumber { get; private set; } = 0;
    private bool resolvingTurn = false;

    [SerializeField] private int roundsPerStage = 3;

    private void Start()
    {
        // Initial wave setup (if not already spawned elsewhere)
        combatManager.SpawnWave(3);
        combatManager.UpdateStageRoundUI();
        BeginPlayerTurn();

        // Clean initial UI state: show cards, hide all status canvases (via menuManager)
        ShowCanvasMM(cardUICanvas);
        HideStatusUI_MM();
    }

    private void BeginPlayerTurn()
    {
        if (CheckLoseAndRestart()) return;

    TurnNumber++;
    State = TurnState.PlayerTurn;

    OnPlayerTurnStarted?.Invoke();
    OnTurnAdvanced?.Invoke();

    // Allow the player to select cards
    cardManager.SetPlayerInputEnabled(true);

    // NEW: Try to deal a hand with at least one playable card.
    bool dealtPlayable = cardManager.TryDealPlayableHand(cardsPerTurn);

    // If there are NO playable cards in the deck at all, trigger a loss.
    if (!dealtPlayable)
    {
        Debug.LogWarning("All remaining cards belong to dead party members. Triggering loss.");
        // Reuse your existing restart logic:
        int idx = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        UnityEngine.SceneManagement.SceneManager.LoadScene(idx);
        return;
    }
    }

    public void EndPlayerTurn()
    {
        if (State != TurnState.PlayerTurn) return;
        if (resolvingTurn) return;
        StartCoroutine(EndPlayerTurnRoutine());
    }

    private IEnumerator EndPlayerTurnRoutine()
    {
        resolvingTurn = true;

        // Disable input while resolving
        cardManager.SetPlayerInputEnabled(false);

        // --- Swap to Status UI immediately (through menu manager) ---
        HideCanvasMM(cardUICanvas);
        ShowStatusUI_MM();
        Canvas.ForceUpdateCanvases(); // flush layout
        yield return null;            // ensure status renders before we start

        // 1) Player cards resolve
        yield return StartCoroutine(cardManager.PlaySelectedCardsSequential());

        if (CheckLoseAndRestart()) { resolvingTurn = false; yield break; }

        // 2) Wave cleared? advance round (heals/reshuffle handled in CardManager)
        EnsureNextWaveIfCleared();
        if (CheckLoseAndRestart()) { resolvingTurn = false; yield break; }

        // 3) Enemy turn
        yield return StartCoroutine(EnemyTurnRoutine());
        if (CheckLoseAndRestart()) { resolvingTurn = false; yield break; }

        // 4) Back to player turn
        BeginPlayerTurn();
        resolvingTurn = false;
    }

    private IEnumerator EnemyTurnRoutine()
    {
        State = TurnState.EnemyTurn;
        OnEnemyTurnStarted?.Invoke();
        OnTurnAdvanced?.Invoke();

        // A) Bleed tick at the start of enemy turn
        foreach (var enemy in combatManager.enemyStored)
        {
            if (enemy == null || enemy.currentHealth <= 0) continue;
            if (enemy.bleed > 0)
            {
                enemy.ApplyBleedDamage();
                if (enemyActDelay > 0f) yield return new WaitForSeconds(enemyActDelay);
            }
        }

        // A2) Curse tick at the start of enemy turn
        foreach (var enemy in combatManager.enemyStored)
        {
            if (enemy == null || enemy.currentHealth <= 0) continue;
            if (enemy.curse > 0)
            {
                enemy.ApplyCurseDamage();
                if (enemyActDelay > 0f) yield return new WaitForSeconds(enemyActDelay);
            }
        }

        if (combatManager.AllEnemiesDead())
        {
            EnsureNextWaveIfCleared();
            yield break;
        }

        // B) Each living enemy acts once
        foreach (var enemy in combatManager.enemyStored)
        {
            if (enemy == null || enemy.currentHealth <= 0) continue;
            try { enemy.TakeTurn(combatManager); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Enemy {enemy?.name} turn error: {e.Message}");
            }
            if (enemyActDelay > 0f) yield return new WaitForSeconds(enemyActDelay);
        }

        // C) Post-action wave check
        EnsureNextWaveIfCleared();
    }

    // ----- Round / Lose logic -----
    private void EnsureNextWaveIfCleared()
    {
        if (!combatManager.AllEnemiesDead()) return;

        if (combatManager.round < roundsPerStage)
        {
            // SAME STAGE -> next round (NO deck return)
            combatManager.round++;
            Debug.Log($"Wave cleared. Advancing to Round {combatManager.round} (Stage {combatManager.stage}).");

            combatManager.SpawnWave(3);
            combatManager.UpdateStageRoundUI();
        }
        else
        {
            // STAGE COMPLETE -> heavy reset + new stage
            combatManager.stage++;
            combatManager.round = 1;
            Debug.Log($"Stage cleared! Advancing to Stage {combatManager.stage}, Round {combatManager.round}.");

            // Heavy reset ONLY at stage start
            cardManager.OnRoundStartReset(combatManager);

            combatManager.SpawnWave(3);
            combatManager.UpdateStageRoundUI();
        }
    }

    private bool CheckLoseAndRestart()
    {
        bool allAlliesDead = combatManager != null && combatManager.AllAlliesDead();
        bool deckEmpty = cardManager != null && cardManager.IsDeckEmpty();

        if (allAlliesDead || deckEmpty)
        {
            Debug.LogWarning($"LOSE! allAlliesDead={allAlliesDead}, deckEmpty={deckEmpty}. Restarting scene…");
            RestartGame();
            return true;
        }
        return false;
    }

    private void RestartGame()
    {
        int idx = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(idx);
    }

    // ===== UI TOGGLING via CombatMenuManager (with safe fallback) =====

    private void ShowCanvasMM(Canvas c)
    {
        if (!c) return;
        if (menuManager != null) menuManager.ShowCanvas(c);   // preferred path
        else
        {
            // fallback if menuManager missing
            c.enabled = true;
            c.gameObject.SetActive(true);
        }
    }

    private void HideCanvasMM(Canvas c)
    {
        if (!c) return;
        if (menuManager != null) menuManager.HideCanvas(c);   // preferred path
        else
        {
            // fallback if menuManager missing
            c.enabled = false;
            c.gameObject.SetActive(false);
        }
    }

    private void ShowStatusUI_MM()
    {
        ShowCanvasMM(statusScreenCanvas);
        ShowCanvasMM(allyHealthCanvas);
        ShowCanvasMM(enemyHealthCanvas);
    }

    private void HideStatusUI_MM()
    {
        HideCanvasMM(statusScreenCanvas);
        HideCanvasMM(allyHealthCanvas);
        HideCanvasMM(enemyHealthCanvas);
    }
}