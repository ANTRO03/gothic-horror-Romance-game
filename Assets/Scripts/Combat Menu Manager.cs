using UnityEngine;

public class CombatMenuManager : MonoBehaviour
{

    // Hide the canvas
    public void HideCanvas(Canvas targetCanvas)
    {
        if (targetCanvas != null)
        {
            targetCanvas.enabled = false;
            Debug.Log($"Canvas '{targetCanvas.name}' set to HIDDEN.");
        }
    }

    // Show the canvas
    public void ShowCanvas(Canvas targetCanvas)
    {
        if (targetCanvas != null)
        {
            targetCanvas.enabled = true;
            Debug.Log($"Canvas '{targetCanvas.name}' set to VISIBLE.");
        }
    }
}
