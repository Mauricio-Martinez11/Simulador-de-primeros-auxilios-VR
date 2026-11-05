using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static bool PreferMixedReality { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRequest() { PreferMixedReality=false; }
    public static bool ConsumeMixedRealityRequest()
    { bool requested=PreferMixedReality; PreferMixedReality=false; return requested; }
    public void LoadMixedRealityTutorial()
    { PreferMixedReality=true; SceneManager.LoadScene("2_Tutorial_RCP"); }
    public void LoadScene(string sceneName)
    {
        PreferMixedReality=false;
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        ExitApplication();
    }
    public static void ExitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
