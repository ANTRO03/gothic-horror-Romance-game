using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TargetSelectionUI : MonoBehaviour
{
    [Header("Scene Refs")]
    [SerializeField] private CombatManager combatManager;

    [Header("UI")]
    [SerializeField] private GameObject panel;            
    [SerializeField] private Button[] buttons;              
    [SerializeField] private TextMeshProUGUI[] labels;       

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (combatManager == null) combatManager = FindFirstObjectByType<CombatManager>();
    }

    public void SelectEnemy(Action<EnemyBase> onPicked)
    {
        if (combatManager == null)
        {
            Debug.LogWarning("[TargetSelectionUI] No CombatManager available.");
            onPicked?.Invoke(null);
            return;
        }

        var candidates = BuildEnemyCandidates(combatManager.enemyStored);
        ShowButtonsForCandidates(
            candidates.Count,
            idx => candidates[idx] != null ? NameHP(candidates[idx].name, candidates[idx].currentHealth) : "—",
            idx =>
            {
                panel.SetActive(false);
                onPicked?.Invoke(candidates[idx]);
            },
            () => { panel.SetActive(false); onPicked?.Invoke(null); }
        );
    }

  
    public void SelectAlly(Action<ButlerBase> onPicked)
    {
        if (combatManager == null)
        {
            Debug.LogWarning("[TargetSelectionUI] No CombatManager available.");
            onPicked?.Invoke(null);
            return;
        }

        var allies = combatManager.EnumerateAllies().ToList();
        var candidates = BuildButlerCandidates(allies);

        ShowButtonsForCandidates(
            candidates.Count,
            idx => candidates[idx] != null ? NameHP(candidates[idx].name, candidates[idx].currentHealth) : "—",
            idx =>
            {
                panel.SetActive(false);
                onPicked?.Invoke(candidates[idx]);
            },
            () => { panel.SetActive(false); onPicked?.Invoke(null); }
        );
    }

   

    private static string NameHP(string name, int hp) => $"{name} (HP {hp})";

    private List<EnemyBase> BuildEnemyCandidates(List<EnemyBase> source)
    {
        var outList = new List<EnemyBase>(3);
        for (int i = 0; i < 3; i++)
        {
            var pick = (source != null && i < source.Count) ? source[i] : null;
            if (pick != null && pick.currentHealth > 0)
                outList.Add(pick);
            else
                outList.Add(null); 
        }
        return outList;
    }

    private List<ButlerBase> BuildButlerCandidates(List<ButlerBase> source)
    {
        var outList = new List<ButlerBase>(3);
        for (int i = 0; i < 3; i++)
        {
            var pick = (source != null && i < source.Count) ? source[i] : null;
            if (pick != null && pick.currentHealth > 0)
                outList.Add(pick);
            else
                outList.Add(null);
        }
        return outList;
    }

  
    private void ShowButtonsForCandidates(
        int candidateCount,
        Func<int, string> labelProvider,
        Action<int> onPick,
        Action onNoValid)
    {
        if (panel == null || buttons == null || buttons.Length < 3)
        {
            Debug.LogWarning("[TargetSelectionUI] Panel or buttons not configured.");
            onNoValid?.Invoke();
            return;
        }

        // Reset UI state
        panel.SetActive(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].onClick.RemoveAllListeners();
            if (labels != null && i < labels.Length && labels[i] != null)
                labels[i].text = "";
            if (buttons[i] != null)
                buttons[i].gameObject.SetActive(false);
        }

        bool anyShown = false;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (i >= candidateCount) break;
            var btn = buttons[i];
            if (btn == null) continue;

            string text = labelProvider(i);
            if (string.IsNullOrEmpty(text) || text == "—")
            {
                btn.gameObject.SetActive(false);
                continue;
            }

            if (labels != null && i < labels.Length && labels[i] != null)
                labels[i].text = text;

            int captured = i;
            btn.onClick.AddListener(() => onPick?.Invoke(captured));
            btn.gameObject.SetActive(true);
            anyShown = true;
        }

        if (!anyShown)
        {
            Debug.Log("[TargetSelectionUI] No valid targets to show.");
            onNoValid?.Invoke();
            panel.SetActive(false);
        }
    }
}