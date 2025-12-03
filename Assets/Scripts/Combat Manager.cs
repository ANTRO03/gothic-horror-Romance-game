using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Linq;

public class CombatManager : MonoBehaviour
{

    [Header("Party")]
    public ButlerBase guard;        //  Tank
    public ButlerBase tailor;       // Support
    public ButlerBase chamberlain;  // DPS

    // Enemies
    public List<EnemyBase> enemyStored = new List<EnemyBase>();

    [Header("Stage / Round Info")]
    public int stage = 1;
    public int round = 1;
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI roundText;

    [Header("Spawning")]
    [SerializeField] private EnemyBase enemyPrefab;
    [SerializeField] private Transform[] enemySpawnPoints;

    [Header("Enemy UI Bars")]
    [SerializeField] private Slider[] enemyHealthBars;

    private void Start()
    {
        UpdateStageRoundUI();
    }

    public IEnumerable<ButlerBase> EnumerateAllies()
    {
        yield return guard;
        yield return tailor;
        yield return chamberlain;
    }

    public List<ButlerBase> GetAliveAllies()
    {
        return EnumerateAllies()
            .Where(a => a != null && a.currentHealth > 0)
            .ToList();
    }


    public bool AllAlliesDead()
    {
        return GetAliveAllies().Count == 0;
    }

    public bool AllEnemiesDead()
    {
        for (int i = 0; i < enemyStored.Count; i++)
        {
            var e = enemyStored[i];
            if (e != null && e.currentHealth > 0) return false;
        }
        return true;
    }

    public void SpawnWave(int count)
    {
        Debug.Log($"[CombatManager] SpawnWave called! Prefab={enemyPrefab} count={count}");


        for (int i = enemyStored.Count - 1; i >= 0; i--)
            if (enemyStored[i] == null) enemyStored.RemoveAt(i);

        if (enemyHealthBars != null)
        {
            for (int i = 0; i < enemyHealthBars.Length; i++)
                if (enemyHealthBars[i] != null) enemyHealthBars[i].gameObject.SetActive(false);
        }

        int toSpawn = Mathf.Min(count, enemySpawnPoints != null ? enemySpawnPoints.Length : count);

        enemyStored.Clear();
        for (int i = 0; i < toSpawn; i++)
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning("CombatManager: No enemyPrefab assigned.");
                break;
            }

            Vector3 pos = enemySpawnPoints != null && i < enemySpawnPoints.Length && enemySpawnPoints[i] != null
                            ? enemySpawnPoints[i].position
                            : Vector3.zero;
            Quaternion rot = enemySpawnPoints != null && i < enemySpawnPoints.Length && enemySpawnPoints[i] != null
                            ? enemySpawnPoints[i].rotation
                            : Quaternion.identity;

            var enemy = Instantiate(enemyPrefab, pos, rot);
            enemy.currentHealth = enemy.maxHealth;

            enemyStored.Add(enemy);


            if (enemyHealthBars != null && i < enemyHealthBars.Length && enemyHealthBars[i] != null)
            {
                enemy.BindHealthBar(enemyHealthBars[i]);
            }
        }

        Debug.Log($"Spawned wave: {toSpawn} enemies. Bound {toSpawn} health bars.");
        UpdateStageRoundUI();
    }

    public void UpdateStageRoundUI()
    {
        if (stageText != null)
            stageText.text = $"{stage}";
        if (roundText != null)
            roundText.text = $"{round}";
    }
}