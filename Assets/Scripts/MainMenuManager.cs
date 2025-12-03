using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
     public void StartGame(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogWarning("StartGame called with null/empty scene name.");
        }
    }

    public void QuitGame()
    {
        Debug.Log("QuitGame called.");

#if UNITY_EDITOR
        // Stop play mode in Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // Quit the built application
        Application.Quit();
#endif
    }
}
