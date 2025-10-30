using UnityEngine;

public class CardBase : MonoBehaviour
{

    public RectTransform Rect;

    public CardType type;
    public ButlerType owner;
    public int ManaPoints;

    public void Awake()
    {
        Rect = GetComponent<RectTransform>();
    }

}
