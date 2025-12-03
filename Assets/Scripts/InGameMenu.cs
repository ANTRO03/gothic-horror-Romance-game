using UnityEngine;

public class InGameMenu : MonoBehaviour
{
    [Header("Menu Root Object")]
    [SerializeField] private GameObject menuRoot;

    [Header("Input Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    private bool menuOpen = false;

    private void Start()
    {
        // Menu should start hidden
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    private void Update()
    {
        // Toggle menu when pressing Escape (or the assigned key)
        if (Input.GetKeyDown(toggleKey))
        {
            if (menuOpen)
                HideMenu();
            else
                ShowMenu();
        }
    }

    /// <summary>
    /// Shows the in-game menu.
    /// </summary>
    public void ShowMenu()
    {
        menuOpen = true;

        if (menuRoot != null)
            menuRoot.SetActive(true);

        // Optional: stop time if you want a pause effect
        // Time.timeScale = 0f;
    }

    /// <summary>
    /// Hides the in-game menu.
    /// </summary>
    public void HideMenu()
    {
        menuOpen = false;

        if (menuRoot != null)
            menuRoot.SetActive(false);

        // Optional: resume time
        // Time.timeScale = 1f;
    }

    /// <summary>
    /// Called by UI button to quit the game.
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("QuitGame called.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
