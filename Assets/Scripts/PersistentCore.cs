using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentCore : MonoBehaviour
{
    private static PersistentCore instance;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (SceneManager.GetActiveScene().name == "Boobstrap")
        {
            StartCoroutine(LoadMainMenu());
        }
    }

    IEnumerator LoadMainMenu()
    {
        yield return new WaitForSeconds(1.5f); 
        SceneManager.LoadScene("1_MainMenu");
    }
}
