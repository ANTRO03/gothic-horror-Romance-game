using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public enum TurnState { PlayerTurn, EnemyTurn }

public class TurnManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CardManager cardManager;
    [SerializeField] private CombatManager combatManager;

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

    private void Start()
    {
        // initial wave
        combatManager.SpawnWave(3);
        BeginPlayerTurn();
    }

    // ---------------- Player Turn ----------------
    private void BeginPlayerTurn()
    {
        TurnNumber++;
        State = TurnState.PlayerTurn;
        OnPlayerTurnStarted?.Invoke();
        OnTurnAdvanced?.Invoke();

        cardManager.SetPlayerInputEnabled(true);

        for (int i = 0; i < cardsPerTurn; i++)
            cardManager.DrawCard();
    }

    // Hook this to your End Turn button
    public void EndPlayerTurn()
    {
        if (State != TurnState.PlayerTurn) return;
        if (resolvingTurn) return;

        StartCoroutine(EndPlayerTurnRoutine());
    }

    private IEnumerator EndPlayerTurnRoutine()
    {
        resolvingTurn = true;
        cardManager.SetPlayerInputEnabled(false);

        // 1) Play selected cards sequentially (waits for EffectFlow.Done from each effect)
        yield return StartCoroutine(cardManager.PlaySelectedCardsSequential());

        // 2) If player cleared the wave, spawn next before enemies act
        EnsureNextWaveIfCleared();

        // 3) Enemy turn (includes Bleed tick at start)
        yield return StartCoroutine(EnemyTurnRoutine());

        // 4) Back to player
        BeginPlayerTurn();
        resolvingTurn = false;
    }

    // ---------------- Enemy Turn ----------------
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

        // If Bleed wiped the wave, spawn and return to player
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
            catch (System.Exception e) { Debug.LogWarning($"Enemy {enemy?.name} turn error: {e.Message}"); }

            if (enemyActDelay > 0f) yield return new WaitForSeconds(enemyActDelay);
        }

        // C) Wave check after enemies act
        EnsureNextWaveIfCleared();
    }

    // ---------------- Helpers ----------------
    private void EnsureNextWaveIfCleared()
    {
        if (combatManager.AllEnemiesDead())
        {
            combatManager.round++;
            Debug.Log($"Wave cleared! Spawning next wave. Round is now {combatManager.round}.");
            combatManager.SpawnWave(3);
            combatManager.UpdateStageRoundUI(); // if you hooked stage/round TMPs
        }
    }
}