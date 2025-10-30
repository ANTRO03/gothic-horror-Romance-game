using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardManager : MonoBehaviour
{
    [Header("UI / Canvas")]
    [SerializeField] private Canvas canvas;

    [Header("Selection Labels (TMP)")]
    [SerializeField] private TextMeshProUGUI firstLabel;
    [SerializeField] private TextMeshProUGUI secondLabel;
    [SerializeField] private TextMeshProUGUI thirdLabel;

    // Up to 3 selections
    [SerializeField] private CardBase[] selected = new CardBase[3];

    // Quick access to label RectTransforms
    private RectTransform[] labelRTs;

    // ----------------------------------------------
    public List<CardBase> deck = new List<CardBase>();
    public List<CardBase> discardPile = new List<CardBase>();
    public List<CardBase> playerHand = new List<CardBase>();

    public Transform[] cardSlots;
    public bool[] availableCardSlots;

    [Header("Confirm Selection Button")]
    public Button confirmButton;

    [Header("Combat/Effects Context")]
    public CombatManager combatManager;

    [Header("Off-screen Return Points")]
    [SerializeField] private Transform deckReturnPoint;
    [SerializeField] private Transform discardPilePoint;

    [Header("Effect Tuning")]
    [Tooltip("Short visual/tempo pause between resolving selected cards.")]
    [SerializeField] private float perCardPause = 0.1f;

    private bool playerInputEnabled = true;

    private void Awake()
    {
        labelRTs = new[]
        {
            firstLabel != null ? firstLabel.rectTransform : null,
            secondLabel != null ? secondLabel.rectTransform : null,
            thirdLabel != null ? thirdLabel.rectTransform : null
        };

        for (int i = 0; i < labelRTs.Length; i++)
            if (labelRTs[i] != null) labelRTs[i].gameObject.SetActive(false);

        // Ensure interactivity is correct at start
        RefreshAllCardInteractivity();

        Debug.Log("CardManager initialized. Waiting for card clicks...");
    }

    // ---------------- Selection ----------------
    public void OnCardClicked(CardBase card)
    {
        if (!playerInputEnabled) return;
        if (card == null)
        {
            Debug.LogWarning("Card click ignored: Card reference was null.");
            return;
        }

        // NEW: block selection if the card's owner butler is dead
        if (!IsOwnerAlive(card))
        {
            Debug.Log($"Blocked selection of '{card.name}' — owner '{card.owner}' is not alive.");
            SetCardInteractable(card, false); // hard-disable to make it clear in UI
            return;
        }

        Debug.Log($"Card clicked: {card.name}");

        int existingIndex = IndexOf(card);
        if (existingIndex != -1)
        {
            selected[existingIndex] = null;
            if (labelRTs[existingIndex] != null)
                labelRTs[existingIndex].gameObject.SetActive(false);
            PrintSelectionArray();
            return;
        }

        int openIndex = FirstOpenIndex();
        if (openIndex == -1)
        {
            Debug.Log("No available slot — all 3 selections are full.");
            return;
        }

        selected[openIndex] = card;
        Debug.Log($"Assigned card '{card.name}' to slot {openIndex} ({SlotName(openIndex)}).");
        PrintSelectionArray();
        PlaceLabelOverCard(openIndex, card);
    }

    private int IndexOf(CardBase card)
    {
        for (int i = 0; i < selected.Length; i++)
            if (selected[i] == card) return i;
        return -1;
    }

    private int FirstOpenIndex()
    {
        for (int i = 0; i < selected.Length; i++)
            if (selected[i] == null) return i;
        return -1;
    }

    private void PlaceLabelOverCard(int index, CardBase card)
    {
        if (card == null || labelRTs[index] == null || canvas == null) return;

        Vector3 screenPos;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            screenPos = card.Rect.position;
        }
        else
        {
            var cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            screenPos = RectTransformUtility.WorldToScreenPoint(cam, card.Rect.position);
        }

        var label = labelRTs[index];
        label.position = screenPos;
        label.gameObject.SetActive(true);

        if (firstLabel && secondLabel && thirdLabel)
        {
            switch (index)
            {
                case 0: firstLabel.text = "First"; break;
                case 1: secondLabel.text = "Second"; break;
                case 2: thirdLabel.text = "Third"; break;
            }
        }

        Debug.Log($"Moved label over card '{card.name}' at slot {index} ({SlotName(index)}).");
    }

    private void PrintSelectionArray()
    {
        string slot0 = selected[0] ? selected[0].name : "Empty";
        string slot1 = selected[1] ? selected[1].name : "Empty";
        string slot2 = selected[2] ? selected[2].name : "Empty";
        Debug.Log($"Current Selection → [1st: {slot0}] [2nd: {slot1}] [3rd: {slot2}]");
    }

    private string SlotName(int index)
    {
        switch (index)
        {
            case 0: return "First";
            case 1: return "Second";
            case 2: return "Third";
            default: return "Unknown";
        }
    }

    // ---------------- Draw ----------------
    public void DrawCard()
    {
        if (deck.Count <= 0)
        {
            Debug.Log("No cards left in deck to draw!");
            return;
        }

        CardBase randCard = deck[UnityEngine.Random.Range(0, deck.Count)];

        for (int i = 0; i < availableCardSlots.Length; i++)
        {
            if (!availableCardSlots[i]) continue;

            if (cardSlots[i] == null)
            {
                Debug.LogWarning($"cardSlots[{i}] is null.");
                continue;
            }

            var cardRT = randCard.Rect;
            var slotRT = cardSlots[i] as RectTransform;

            randCard.gameObject.SetActive(true);

            if (slotRT != null)
            {
                cardRT.SetParent(slotRT, worldPositionStays: false);
                cardRT.anchoredPosition = Vector2.zero;
                cardRT.localRotation = Quaternion.identity;
                cardRT.localScale = Vector3.one;
            }
            else
            {
                randCard.transform.position = cardSlots[i].position;
                randCard.transform.rotation = cardSlots[i].rotation;
                randCard.transform.localScale = Vector3.one;
            }

            availableCardSlots[i] = false;
            deck.Remove(randCard);
            playerHand.Add(randCard);

            // NEW: immediately reflect whether this card can be used (owner alive?)
            SetCardInteractable(randCard, IsOwnerAlive(randCard));

            Debug.Log($"Drew '{randCard.name}' → placed into slot {i}. Deck: {deck.Count}, Hand: {playerHand.Count}");
            return;
        }

        Debug.Log("No available hand slots to place a drawn card.");
    }

    // ---------------- Play (sequential, type-driven) ----------------
    public IEnumerator PlaySelectedCardsSequential()
    {
        for (int i = 0; i < selected.Length; i++)
        {
            var card = selected[i];
            if (card == null) continue;

            try
            {
                ExecuteCardByType(card);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error resolving '{card?.name}' ({card?.type}): {e.Message}");
            }

            // Cleanup this card → DISCARD
            if (playerHand.Contains(card)) playerHand.Remove(card);
            if (!discardPile.Contains(card)) discardPile.Add(card);

            FreeHandSlotFor(card);

            if (discardPilePoint != null)
                card.transform.position = discardPilePoint.position;

            if (labelRTs != null && i >= 0 && i < labelRTs.Length && labelRTs[i] != null)
                labelRTs[i].gameObject.SetActive(false);

            selected[i] = null;
            card.gameObject.SetActive(false);

            if (perCardPause > 0f)
                yield return new WaitForSeconds(perCardPause);
            else
                yield return null;
        }

        // Return remaining hand to deck (+ MP grant handled elsewhere if you added it)
        var remaining = new List<CardBase>(playerHand);
        foreach (var card in remaining)
        {
            if (card == null) continue;
            FreeHandSlotFor(card);
            if (deckReturnPoint != null) card.transform.position = deckReturnPoint.position;
            playerHand.Remove(card);
            if (!deck.Contains(card)) deck.Add(card);
            card.gameObject.SetActive(false);
        }

        // After turn resolves, refresh interactivity in case a butler died/revived during actions
        RefreshAllCardInteractivity();

        Debug.Log($"PlaySelectedCardsSequential → Discard: {discardPile.Count}, Deck: {deck.Count}, Hand: {playerHand.Count}");
    }

    // ---------------- CardType → Effect mapping ----------------
    private void ExecuteCardByType(CardBase card)
    {
        if (combatManager == null)
        {
            Debug.LogWarning("No CombatManager assigned — cannot resolve card effects.");
            return;
        }

        EnemyBase enemy = FirstAliveEnemy();
        List<EnemyBase> allEnemies = AliveEnemies();
        ButlerBase lowestAlly = LowestHealthAlly();
        ButlerBase guard = combatManager.guard;
        ButlerBase tailor = combatManager.tailor;
        ButlerBase dps = combatManager.chamberlain;

        void Damage(EnemyBase e, int dmg)
        {
            if (e == null || e.currentHealth <= 0) return;
            e.TakeDamage(Mathf.Max(0, dmg));
        }
        void Heal(ButlerBase b, int amt)
        {
            if (b == null || b.currentHealth <= 0) return;
            b.Heal(Mathf.Max(0, amt));
        }
        void BuffStrength(ButlerBase b, bool on = true) { if (b == null) return; if (on) b.SetStrengthTrue(); else b.SetStrengthFalse(); }
        void Invincible(ButlerBase b, bool on = true) { if (b == null) return; if (on) b.SetInvincibilityTrue(); else b.SetInvincibilityFalse(); }
        void MarkTarget(ButlerBase b, bool on = true) { if (b == null) return; if (on) b.SetTargetTrue(); else b.SetTargetFalse(); }

        int baseDmg = 4;
        int smallHeal = 3;
        int aoeDmg = 2;
        int bigHeal = 6;

        switch (card.type)
        {
            case CardType.CatchStitch:
                Heal(lowestAlly ?? tailor ?? guard ?? dps, smallHeal);
                break;
            case CardType.ChainStitch:
                foreach (var a in combatManager.GetAliveAllies()) Heal(a, 2);
                break;
            case CardType.BackStitch:
                BuffStrength(dps ?? lowestAlly, true);
                break;
            case CardType.Unseam:
                if (enemy != null) { Damage(enemy, baseDmg); enemy.bleed += 1; }
                break;
            case CardType.Resolve:
                var targetA = lowestAlly ?? tailor ?? guard ?? dps;
                if (targetA != null)
                {
                    if (targetA.bleed > 0) targetA.bleed--;
                    else if (targetA.curse > 0) targetA.curse--;
                    else if (targetA.timeBomb > 0) targetA.timeBomb--;
                    Heal(targetA, 2);
                }
                break;

            case CardType.lance:
                int bonus = (dps != null && dps.strength) || (guard != null && guard.strength) ? 2 : 0;
                Damage(enemy, baseDmg + bonus);
                break;
            case CardType.Chivalry:
                MarkTarget(guard, true);
                BuffStrength(guard, true);
                break;
            case CardType.Aegis:
                Invincible(lowestAlly ?? guard, true);
                break;
            case CardType.Denounce:
                if (enemy != null) { enemy.curse += 1; Damage(enemy, 1); }
                break;
            case CardType.Levy:
                foreach (var e in allEnemies) Damage(e, aoeDmg);
                break;
            case CardType.Patronage:
                foreach (var a in combatManager.GetAliveAllies())
                {
                    a.currentMP = Mathf.Min(a.maxMP, a.currentMP + 1);
                    Heal(a, 2);
                }
                break;
            case CardType.RoyalRepreve:
                foreach (var a in combatManager.GetAliveAllies()) Heal(a, bigHeal);
                if (guard != null) Invincible(guard, true);
                break;
            default:
                Debug.LogWarning($"Unhandled CardType '{card.type}'. No effect executed.");
                break;
        }
    }

    // ---------------- Owner/Butler helpers ----------------
    private ButlerBase ButlerFor(ButlerType owner)
    {
        if (combatManager == null) return null;
        switch (owner)
        {
            case ButlerType.Guard:        return combatManager.guard;
            case ButlerType.Tailor:       return combatManager.tailor;
            case ButlerType.Chamberlain:  return combatManager.chamberlain;
            default:                      return null;
        }
    }

    private bool IsOwnerAlive(CardBase card)
    {
        var b = ButlerFor(card.owner);
        return b != null && b.currentHealth > 0;
    }

    // ---------------- UI Interactivity helpers ----------------
    public void RefreshAllCardInteractivity()
    {
        // Hand cards are the ones visible & interactable
        foreach (var c in playerHand)
            if (c != null) SetCardInteractable(c, IsOwnerAlive(c));

        // If you also surface deck/discard in UI, uncomment:
        // foreach (var c in deck) if (c != null) SetCardInteractable(c, IsOwnerAlive(c));
        // foreach (var c in discardPile) if (c != null) SetCardInteractable(c, IsOwnerAlive(c));
    }

    private void SetCardInteractable(CardBase card, bool enabled)
    {
        if (card == null) return;

        // Try a Button first (most common)
        var btn = card.GetComponentInChildren<Button>(true);
        if (btn != null) btn.interactable = enabled;

        // Optional: also dim and block raycasts if CanvasGroup exists
        var cg = card.GetComponentInChildren<CanvasGroup>(true);
        if (cg != null)
        {
            cg.alpha = enabled ? 1f : 0.5f;
            cg.blocksRaycasts = enabled;
            cg.interactable = enabled;
        }
    }

    // ---------------- General Helpers ----------------
    private int FindNearestSlotIndex(CardBase card)
    {
        if (card == null || cardSlots == null || cardSlots.Length == 0) return -1;

        int bestIndex = -1;
        float bestDist = float.MaxValue;

        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (cardSlots[i] == null) continue;
            float d = Vector3.SqrMagnitude(card.transform.position - cardSlots[i].position);
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private void FreeHandSlotFor(CardBase card)
    {
        int idx = FindNearestSlotIndex(card);
        if (idx >= 0 && idx < availableCardSlots.Length)
            availableCardSlots[idx] = true;
    }

    public void SetPlayerInputEnabled(bool enabled)
    {
        playerInputEnabled = enabled;
        if (confirmButton) confirmButton.interactable = enabled;
    }

    private EnemyBase FirstAliveEnemy()
    {
        if (combatManager == null) return null;
        foreach (var e in combatManager.enemyStored)
            if (e != null && e.currentHealth > 0) return e;
        return null;
    }

    private List<EnemyBase> AliveEnemies()
    {
        if (combatManager == null) return new List<EnemyBase>();
        return combatManager.enemyStored.Where(e => e != null && e.currentHealth > 0).ToList();
    }

    private ButlerBase LowestHealthAlly()
    {
        if (combatManager == null) return null;
        var alive = combatManager.GetAliveAllies();
        if (alive.Count == 0) return null;
        return alive.OrderBy(a => (float)a.currentHealth / Mathf.Max(1, a.maxHealth)).FirstOrDefault();
    }
}