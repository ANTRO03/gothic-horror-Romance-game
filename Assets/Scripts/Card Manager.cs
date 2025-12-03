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

    // Up to 3 selections per turn
    [SerializeField] private CardBase[] selected = new CardBase[3];
    private RectTransform[] labelRTs;

    [Header("Card Collections")]
    public List<CardBase> deck = new List<CardBase>();
    public List<CardBase> discardPile = new List<CardBase>();
    public List<CardBase> playerHand = new List<CardBase>();

    [Header("Hand Slots")]
    public Transform[] cardSlots;          // assign in Inspector
    public bool[] availableCardSlots;      // same length as cardSlots

    [Header("Confirm Selection Button")]
    [SerializeField] public Button confirmButton;

    [Header("External References")]
    [SerializeField] public CombatManager combatManager;     // assign in Inspector
    [SerializeField] private TurnManager turnManager;        // assign in Inspector

    [Header("Off-screen Return Points (optional)")]
    [SerializeField] private Transform deckReturnPoint;
    [SerializeField] private Transform discardPilePoint;

    [Header("Effect Tuning")]
    [Tooltip("Short visual/tempo pause between resolving selected cards.")]
    [SerializeField] private float perCardPause = 0.1f;

    [Header("Targeting (Enemy)")]
    [SerializeField] private Button[] enemyTargetButtons;   // size = number of enemy UI buttons (e.g., 3)
    [SerializeField] private Image[] enemyTargetIcons;     // small icon per enemy; set Active when targeted

    private EnemyBase selectedEnemyTarget;

    [Header("Ultimate Cards (one per Butler)")]
    [SerializeField] private CardBase guardUltimateCard;
    [SerializeField] private CardBase tailorUltimateCard;
    [SerializeField] private CardBase chamberlainUltimateCard;

    private bool playerInputEnabled = true;

    private void Awake()
    {
        labelRTs = new[]
        {
            firstLabel ? firstLabel.rectTransform : null,
            secondLabel ? secondLabel.rectTransform : null,
            thirdLabel ? thirdLabel.rectTransform : null
        };
        for (int i = 0; i < labelRTs.Length; i++)
            if (labelRTs[i] != null) labelRTs[i].gameObject.SetActive(false);

        // Hook up confirm button → TurnManager flow
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmPressed);
            confirmButton.gameObject.SetActive(false); // invisible until a selection exists
            confirmButton.interactable = false;
        }

        if (guardUltimateCard) guardUltimateCard.gameObject.SetActive(false);
        if (tailorUltimateCard) tailorUltimateCard.gameObject.SetActive(false);
        if (chamberlainUltimateCard) chamberlainUltimateCard.gameObject.SetActive(false);

        RefreshAllCardInteractivity();
        Debug.Log("CardManager ready.");

    }

    private void Update()
    {
        if (selectedEnemyTarget != null && selectedEnemyTarget.currentHealth <= 0)
        {
            selectedEnemyTarget = null;
        }

        UpdateUltimateVisibility();
    }

    // ---------------- Selection ----------------
    public void OnCardClicked(CardBase card)
    {
        if (!playerInputEnabled) return;
        if (card == null) return;

        // Block selecting cards owned by KO'd butlers
        if (!IsOwnerAlive(card))
        {
            SetCardInteractable(card, false);
            UpdateConfirmButtonVisibility();
            return;
        }

        int existing = IndexOf(card);
        if (existing != -1)
        {
            selected[existing] = null;
            if (labelRTs[existing] != null) labelRTs[existing].gameObject.SetActive(false);
            UpdateConfirmButtonVisibility();
            return;
        }

        int open = FirstOpenIndex();
        if (open == -1)
        {
            Debug.Log("Selection full (3).");
            UpdateConfirmButtonVisibility();
            return;
        }

        selected[open] = card;
        PlaceLabelOverCard(open, card);
        UpdateConfirmButtonVisibility();
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
            screenPos = card.Rect.position;
        else
            screenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera ?? Camera.main, card.Rect.position);

        var label = labelRTs[index];
        label.position = screenPos;
        label.gameObject.SetActive(true);

        if (firstLabel && secondLabel && thirdLabel)
        {
            if (index == 0) firstLabel.text = "First";
            else if (index == 1) secondLabel.text = "Second";
            else if (index == 2) thirdLabel.text = "Third";
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
            if (cardSlots[i] == null) continue;

            var cardRT = randCard.Rect;
            var slotRT = cardSlots[i] as RectTransform;

            randCard.gameObject.SetActive(true);

            if (slotRT != null)
            {
                cardRT.SetParent(slotRT, false);
                cardRT.anchoredPosition = Vector2.zero;
                cardRT.localRotation = Quaternion.identity;
                cardRT.localScale = Vector3.one;
            }
            else
            {
                randCard.transform.SetParent(cardSlots[i], false);
                randCard.transform.localPosition = Vector3.zero;
                randCard.transform.localRotation = Quaternion.identity;
                randCard.transform.localScale = Vector3.one;
            }

            availableCardSlots[i] = false;
            deck.Remove(randCard);
            playerHand.Add(randCard);

            // Immediately reflect owner HP (disable/grey if owner dead)
            SetCardInteractable(randCard, IsOwnerAlive(randCard));
            UpdateConfirmButtonVisibility();
            return;
        }

        Debug.Log("No available hand slots to place a drawn card.");
    }

    // ---------------- Confirm → TurnManager plays/advances ----------------
    public void OnConfirmPressed()
    {
        if (!HasAtLeastOneSelected())
        {
            Debug.Log("Confirm pressed but no cards selected.");
            return;
        }
        if (turnManager == null)
        {
            Debug.LogWarning("TurnManager not assigned on CardManager!");
            return;
        }

        // Lock hand input while TurnManager resolves the turn
        SetPlayerInputEnabled(false);

        // Hand control to TurnManager. It will call PlaySelectedCardsSequential(), then run enemy turn, etc.
        turnManager.EndPlayerTurn();
    }

    // TurnManager will call this to resolve the selected cards
    public IEnumerator PlaySelectedCardsSequential()
    {
        // 1) Resolve selected (played) cards → discard
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
                Debug.LogWarning($"Card '{card?.name}' error: {e.Message}");
            }

            bool isUlt = IsUltimateCard(card);

            if (!isUlt)
            {
                // Normal card: move played card → discard
                if (playerHand.Contains(card)) playerHand.Remove(card);
                if (!discardPile.Contains(card)) discardPile.Add(card);

                FreeHandSlotFor(card);

                if (discardPilePoint != null)
                    card.transform.position = discardPilePoint.position;

                card.gameObject.SetActive(false);
            }
            else
            {
                // Ultimate: consume MP and hide, but do NOT touch deck/hand/discard
                OnUltimatePlayed(card);
            }

            // Clear selection label either way
            if (labelRTs != null && i >= 0 && i < labelRTs.Length && labelRTs[i] != null)
                labelRTs[i].gameObject.SetActive(false);

            selected[i] = null;

            if (perCardPause > 0f) yield return new WaitForSeconds(perCardPause);
            else yield return null;
        }

        // 2) Return remaining (UNPLAYED) hand cards → deck and grant MP
        var remaining = new List<CardBase>(playerHand);
        foreach (var c in remaining)
        {
            if (c == null) continue;

            GrantMPForCardOwner(c);

            FreeHandSlotFor(c);
            if (deckReturnPoint != null) c.transform.position = deckReturnPoint.position;
            playerHand.Remove(c);
            if (!deck.Contains(c)) deck.Add(c);
            c.gameObject.SetActive(false);
        }

        RefreshAllCardInteractivity();

        if (confirmButton != null) confirmButton.gameObject.SetActive(false);

        Debug.Log($"Selected cards resolved. Discard: {discardPile.Count}, Deck: {deck.Count}, Hand: {playerHand.Count}");
    }


    // ---------------- CardType Effects (auto-targeting) ----------------
    private void ExecuteCardByType(CardBase card)
    {
        if (combatManager == null) return;

        EnemyBase enemy = FirstAliveEnemy();
        List<EnemyBase> allEnemies = AliveEnemies();
        ButlerBase lowestAlly = LowestHealthAlly();
        ButlerBase guard = combatManager.guard;
        ButlerBase tailor = combatManager.tailor;
        ButlerBase chamberlain = combatManager.chamberlain;

        void Damage(EnemyBase e, int dmg) { if (e == null || e.currentHealth <= 0) return; e.TakeDamage(Mathf.Max(0, dmg)); }
        void Heal(ButlerBase b, int amt) { if (b == null || b.currentHealth <= 0) return; b.Heal(Mathf.Max(0, amt)); }

        int baseDmg = 4, smallHeal = 3, aoeDmg = 2, bigHeal = 6;

        switch (card.type)
        {
            case CardType.CatchStitch:
                {
                    // Tailor attack: single target. +2 if Tailor has Strength, then consume it.
                    int bonus = StrengthBonusForOwnerAndConsume(tailor);
                    var target = LowestHpEnemy_FirstInArrayTieBreak();
                    if (target != null)
                    {
                        target.TakeDamage(5 + bonus);
                        target.bleed += 3;
                    }
                    else
                    {
                        Debug.Log("Catch Stitch: no valid enemy target.");
                    }
                    break;
                }

            case CardType.ChainStitch:
                {
                    // Tailor attack: AOE. +2 if Tailor has Strength (applied to each hit), then consume it.
                    int bonus = StrengthBonusForOwnerAndConsume(tailor);
                    foreach (var e in AliveEnemies())
                    {
                        e.TakeDamage(2 + bonus);
                        e.bleed += 1;
                    }
                    break;
                }

            case CardType.BackStitch:
                {
                    // Tailor attack: single target. +2 if Tailor has Strength, then consume it.
                    int bonus = StrengthBonusForOwnerAndConsume(tailor);
                    var target = LowestHpEnemy_FirstInArrayTieBreak();
                    if (target != null)
                    {
                        target.TakeDamage(3 + bonus);
                        target.bleed += 5;
                    }
                    else
                    {
                        Debug.Log("Back Stitch: no valid enemy target.");
                    }
                    break;
                }

            case CardType.Unseam:
                {
                    // ULTIMATE: Board-wide bleed detonation
                    int totalBleed = 0;

                    // Count bleed on all living enemies
                    foreach (var e in combatManager.enemyStored)
                    {
                        if (e == null) continue;
                        if (e.bleed > 0)
                            totalBleed += Mathf.Max(0, e.bleed);
                    }


                    if (totalBleed <= 0)
                    {
                        Debug.Log("Unseam (ULT): No bleed stacks on the board.");
                    }
                    else
                    {
                        // Deal that much damage to all living enemies
                        foreach (var e in AliveEnemies())
                        {
                            e.TakeDamage(totalBleed);
                        }
                        Debug.Log($"Unseam (ULT): Dealt {totalBleed} damage to all enemies, then cleared all bleed.");
                    }

                    // Clear ALL bleed stacks (allies + enemies)
                    foreach (var e in combatManager.enemyStored)
                    {
                        if (e == null) continue;
                        e.bleed = 0;
                    }

                    break;
                }

            case CardType.Resolve:
                {
                    // Guard utility (no damage) — no Strength consumption here.
                    if (guard != null)
                    {
                        guard.AddShield(1);
                        guard.Heal(2);
                        Debug.Log("Resolve: Guard +1 shield and healed 2.");
                    }
                    else
                    {
                        Debug.Log("Resolve: Guard not found.");
                    }
                    break;
                }

            case CardType.lance:
                {
                    // Guard attack: AOE. +2 if Guard has Strength (applied to each hit), then consume it.
                    int bonus = StrengthBonusForOwnerAndConsume(guard);
                    foreach (var e in AliveEnemies())
                        e.TakeDamage(2 + bonus);

                    if (guard != null)
                    {
                        guard.SetTargetTrue(); // your “gain target” flag
                        Debug.Log("Lance: Guard gained target flag.");
                    }
                    break;
                }

            case CardType.Chivalry:
                {
                    // Guard attack: single target. +2 if Guard has Strength, then consume it.
                    int bonus = StrengthBonusForOwnerAndConsume(guard);
                    var target = LowestHpEnemy_FirstInArrayTieBreak();
                    if (target != null)
                    {
                        target.TakeDamage(3 + bonus);
                    }
                    else
                    {
                        Debug.Log("Chivalry: no valid enemy target.");
                    }

                    if (guard != null)
                    {
                        guard.AddShield(1);
                        Debug.Log("Chivalry: Guard gained 1 shield.");
                    }
                    break;
                }

            case CardType.Aegis:
                {
                    // ULTIMATE: Teamwide shield + Guard heal
                    var allies = combatManager.EnumerateAllies();
                    foreach (var a in allies)
                    {
                        if (a == null || a.currentHealth <= 0) continue;
                        a.AddShield(1);
                    }

                    if (guard != null && guard.currentHealth > 0)
                    {
                        guard.Heal(3);
                    }

                    Debug.Log("Aegis (ULT): All allies gained 1 shield; Guard healed for 3.");
                    break;
                }


            case CardType.Denounce:
                {
                    // Denounce — Single target: apply 2 Curse (no damage; does NOT consume Strength)
                    var target = LowestHpEnemy_FirstInArrayTieBreak();
                    if (target != null)
                    {
                        target.curse += 2;
                        Debug.Log("Denounce: Applied 2 Curse to lowest-HP enemy.");
                    }
                    else
                    {
                        Debug.Log("Denounce: no valid enemy target.");
                    }
                    break;
                }

            case CardType.Levy:
                {
                    // Levy — Single target: deal 2 (+Strength) damage, then heal Chamberlain
                    // for an equal amount (lifesteal-style).
                    int bonus = StrengthBonusForOwnerAndConsume(chamberlain); // +2 if Strength, then consume
                    var target = LowestHpEnemy_FirstInArrayTieBreak();
                    if (target != null)
                    {
                        int dmg = 2 + bonus;
                        target.TakeDamage(dmg);
                        if (chamberlain != null) chamberlain.Heal(dmg);
                        Debug.Log($"Levy: dealt {dmg} to lowest-HP enemy and healed Chamberlain {dmg}.");
                    }
                    else
                    {
                        Debug.Log("Levy: no valid enemy target.");
                    }
                    break;
                }

            case CardType.Patronage:
                {
                    // Patronage — Party buff: grant Strength to everyone and heal each for 2.
                    var allies = combatManager.GetAliveAllies();
                    foreach (var a in allies)
                    {
                        a.strength = true;  // give Strength
                        a.Heal(3);          // heal 3
                    }
                    Debug.Log("Patronage: Granted Strength to all allies and healed each for 2.");
                    break;
                }

            case CardType.RoyalRepreve:
                {
                    // ULTIMATE: Full-board cleanse of Bleed & Curse, plus big party heal
                    var allies = combatManager.EnumerateAllies();
                    foreach (var a in allies)
                    {
                        if (a == null) continue;

                        // Clear party debuffs
                        a.bleed = 0;
                        a.curse = 0;

                        // Heal each party member for 5
                        if (a.currentHealth > 0)
                            a.Heal(5);
                    }

                    Debug.Log("Royal Repreve (ULT): Cleared all Bleed & Curse and healed the party for 5.");
                    break;
                }

            default:
                Debug.LogWarning($"Unhandled CardType '{card.type}'.");
                break;
        }
    }

    // ---------------- Owner/Butler helpers ----------------
    private ButlerBase ButlerFor(ButlerType owner)
    {
        if (combatManager == null) return null;
        return owner switch
        {
            ButlerType.Guard => combatManager.guard,
            ButlerType.Tailor => combatManager.tailor,
            ButlerType.Chamberlain => combatManager.chamberlain,
            _ => null
        };
    }

    private bool IsOwnerAlive(CardBase card)
    {
        var b = ButlerFor(card.owner);
        return b != null && b.currentHealth > 0;
    }

    // ---------------- UI Interactivity helpers ----------------
    public void RefreshAllCardInteractivity()
    {
        foreach (var c in playerHand)
            if (c != null) SetCardInteractable(c, IsOwnerAlive(c));
    }

    private void SetCardInteractable(CardBase card, bool enabled)
    {
        if (card == null) return;

        var btn = card.GetComponentInChildren<Button>(true);
        if (btn != null) btn.interactable = enabled;

        var cg = card.GetComponentInChildren<CanvasGroup>(true);
        if (cg != null)
        {
            cg.alpha = enabled ? 1f : 0.5f;
            cg.blocksRaycasts = enabled;
            cg.interactable = enabled;
        }
    }

    public void SetPlayerInputEnabled(bool enabled)
    {
        playerInputEnabled = enabled;
        UpdateConfirmButtonVisibility();
    }

    private bool HasAtLeastOneSelected() => selected.Any(c => c != null);

    private void UpdateConfirmButtonVisibility()
    {
        if (confirmButton == null) return;
        bool show = HasAtLeastOneSelected();
        if (confirmButton.gameObject.activeSelf != show)
            confirmButton.gameObject.SetActive(show);
        confirmButton.interactable = show && playerInputEnabled;
    }

    private int FindNearestSlotIndex(CardBase card)
    {
        if (card == null || cardSlots == null || cardSlots.Length == 0) return -1;

        int bestIndex = -1;
        float bestDist = float.MaxValue;

        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (cardSlots[i] == null) continue;
            float d = Vector3.SqrMagnitude(card.transform.position - cardSlots[i].position);
            if (d < bestDist) { bestDist = d; bestIndex = i; }
        }
        return bestIndex;
    }

    private void FreeHandSlotFor(CardBase card)
    {
        int idx = FindNearestSlotIndex(card);
        if (idx >= 0 && idx < availableCardSlots.Length)
            availableCardSlots[idx] = true;
    }

    // ---------------- Enemy/Ally queries ----------------
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

    public EnemyBase CurrentTargetOrLowest()
    {
        if (selectedEnemyTarget != null && selectedEnemyTarget.currentHealth > 0)
            return selectedEnemyTarget;

        return LowestHealthEnemy(); // fallback if no explicit target
    }

    private EnemyBase LowestHealthEnemy()
    {
        var alive = AliveEnemies(); // you already have AliveEnemies() in CardManager
        if (alive.Count == 0) return null;
        return alive.OrderBy(e => e.currentHealth).FirstOrDefault();
    }

    // ---------------- Round/Deck helpers used by TurnManager ----------------
    public bool IsDeckEmpty() => deck == null || deck.Count == 0;

    // Any card in hand whose owner is alive?
    public bool HandHasPlayableCard()
    {
        return playerHand.Any(c => c != null && IsOwnerAlive(c));
    }

    // Any card in DECK whose owner is alive?
    public bool HasPlayableCardInDeck()
    {
        if (deck == null || deck.Count == 0) return false;
        return deck.Any(c => c != null && IsOwnerAlive(c));
    }

    public bool NoPlayableCardsInDeck() => !HasPlayableCardInDeck();

    // Return entire current hand back to the deck (NO MP gain), clear slots.
    public void ReturnHandToDeck_NoMP()
    {
        var copy = new List<CardBase>(playerHand);
        foreach (var c in copy)
        {
            if (c == null) continue;

            FreeHandSlotFor(c);
            playerHand.Remove(c);

            if (!deck.Contains(c))
                deck.Add(c);

            c.gameObject.SetActive(false);
        }

        RefreshAllCardInteractivity();
    }

    // Try to deal a hand that has at least one playable card.
    // Returns true if a playable hand was dealt,
    // false if there are NO playable cards left in the deck at all.
    public bool TryDealPlayableHand(int cardsToDraw)
    {
        // If there are no playable cards in the deck, tell caller so it can trigger a loss.
        if (!HasPlayableCardInDeck())
            return false;

        const int maxRedraws = 5;

        for (int attempt = 0; attempt < maxRedraws; attempt++)
        {
            // Clear any previous attempt’s hand without giving MP for returns.
            ReturnHandToDeck_NoMP();

            // Draw a fresh hand
            for (int i = 0; i < cardsToDraw; i++)
            {
                DrawCard();
            }

            // If at least one card belongs to a living butler, we’re good.
            if (HandHasPlayableCard())
                return true;
        }

        // We know there ARE playable cards in deck, so reaching here should be basically impossible,
        // but to avoid infinite loops we just accept the last hand.
        return true;
    }

    public void OnRoundStartReset(CombatManager cm)
    {
        try
        {
            // --- 0) Ensure lists exist ---
            if (deck == null) deck = new List<CardBase>();
            if (discardPile == null) discardPile = new List<CardBase>();
            if (playerHand == null) playerHand = new List<CardBase>();

            // --- 2) Discard -> Deck using a COPY (avoid structural issues) ---
            if (discardPile.Count > 0)
            {
                // Remove nulls first from discard
                discardPile.RemoveAll(c => c == null);
                if (discardPile.Count > 0) deck.AddRange(new List<CardBase>(discardPile));
                discardPile.Clear();
            }

            // --- 3) Remove nulls from deck (defense against destroyed cards) ---
            deck.RemoveAll(c => c == null);

            // --- 4) Shuffle (only if >= 2) ---
            int n = deck.Count;
            if (n >= 2)
            {
                for (int i = n - 1; i > 0; i--)
                {
                    // int overload of Random.Range is max-exclusive
                    int j = UnityEngine.Random.Range(0, i + 1); // yields [0..i]
                    var tmp = deck[i];
                    deck[i] = deck[j];
                    deck[j] = tmp;
                }
            }

            // --- 5) Heal/Revive party for new stage ---
            if (cm != null)
            {
                var party = new[] { cm.guard, cm.tailor, cm.chamberlain };
                foreach (var b in party)
                {
                    if (b == null) continue;
                    if (b.currentHealth > 0)
                    {
                        int toFull = Mathf.Max(0, b.maxHealth - b.currentHealth);
                        if (toFull > 0) b.Heal(toFull);
                    }
                    else
                    {
                        int half = Mathf.CeilToInt(b.maxHealth * 0.5f);
                        b.Heal(half);
                    }
                }
            }

            // --- 6) UI / Interactivity reset for the new stage ---
            RefreshAllCardInteractivity();
            if (confirmButton != null)
            {
                confirmButton.interactable = false;
                confirmButton.gameObject.SetActive(false);
            }

            Debug.Log($"[Stage Reset] Deck:{deck.Count} Discard:{discardPile.Count} Hand:{playerHand.Count}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"OnRoundStartReset failed safely: {e.Message}\n{e.StackTrace}");
            // Do NOT rethrow — allow TurnManager to continue and spawn the next wave.
        }
    }

    // Grants MP to the owner of a card (clamped to maxMP).
    private void GrantMPForCardOwner(CardBase card)
    {
        if (card == null || combatManager == null) return;

        ButlerBase owner = card.owner switch
        {
            ButlerType.Guard => combatManager.guard,
            ButlerType.Tailor => combatManager.tailor,
            ButlerType.Chamberlain => combatManager.chamberlain,
            _ => null
        };
        if (owner == null) return;

        // if you want to skip KO’d owners, add:
        // if (owner.currentHealth <= 0) return;

        // one-line, UI-safe MP gain
        owner.GainMP(card.ManaPoints);
    }

    private EnemyBase LowestHpEnemy_FirstInArrayTieBreak()
    {
        if (combatManager == null || combatManager.enemyStored == null || combatManager.enemyStored.Count == 0)
            return null;

        int minHP = int.MaxValue;

        // Pass 1: find min HP among living enemies
        for (int i = 0; i < combatManager.enemyStored.Count; i++)
        {
            var e = combatManager.enemyStored[i];
            if (e == null || e.currentHealth <= 0) continue;
            if (e.currentHealth < minHP) minHP = e.currentHealth;
        }
        if (minHP == int.MaxValue) return null; // no living enemies

        // Pass 2: return the first enemy in array with that HP (tie-breaker)
        for (int i = 0; i < combatManager.enemyStored.Count; i++)
        {
            var e = combatManager.enemyStored[i];
            if (e == null || e.currentHealth <= 0) continue;
            if (e.currentHealth == minHP) return e;
        }

        return null;
    }

    private int StrengthBonusForOwnerAndConsume(ButlerBase owner)
    {
        if (owner != null && owner.strength)
        {
            owner.strength = false; // consume the buff
            return 2;
        }
        return 0;
    }

    private void UpdateUltimateVisibility()
{
    if (combatManager == null) return;

    UpdateUltimateForButler(combatManager.guard, guardUltimateCard);
    UpdateUltimateForButler(combatManager.tailor, tailorUltimateCard);
    UpdateUltimateForButler(combatManager.chamberlain, chamberlainUltimateCard);
}

    private void UpdateUltimateForButler(ButlerBase butler, CardBase ultCard)
    {
        if (ultCard == null) return;

        bool shouldShow = butler != null
            && butler.currentHealth > 0
            && butler.currentMP >= butler.maxMP;

        if (ultCard.gameObject.activeSelf != shouldShow)
        {
            ultCard.gameObject.SetActive(shouldShow);
        }

        if (shouldShow)
        {
            SetCardInteractable(ultCard, true);
        }
        else
        {
            SetCardInteractable(ultCard, false);
        }
    }

    private bool IsUltimateCard(CardBase card)
    {
        return card != null &&
               (card == guardUltimateCard ||
                card == tailorUltimateCard ||
                card == chamberlainUltimateCard);
    }

    private void OnUltimatePlayed(CardBase card)
    {
        if (card == null || combatManager == null) return;

        // Consume MP from the card's owner
        var owner = ButlerFor(card.owner);
        if (owner != null)
        {
            owner.ConsumeAllMP();
        }

        // Hide the ult card for now; UpdateUltimateVisibility() will show it again
        // once MP is refilled back to max.
        card.gameObject.SetActive(false);

        Debug.Log($"Ultimate {card.type} used by {owner?.name}. MP reset.");
    }


}

