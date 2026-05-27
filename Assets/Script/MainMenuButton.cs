using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void PlayTeachingMode()
    {
        SceneManager.LoadScene("TeachingScene");
    }

    public void PlayPuzzleMode()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void PlayEasy() { 
        GameSettings.difficulty = 0;
        SceneManager.LoadScene("SampleScene");
    }

    public void PlayHard() {
        GameSettings.difficulty = 1;
        SceneManager.LoadScene("SampleScene");
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}
