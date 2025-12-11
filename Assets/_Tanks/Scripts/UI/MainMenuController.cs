// File: MainMenuController.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    // Gắn vào nút "Local Gameplay"
    public void OnLocalGameplayClicked()
    {
        Debug.Log("Loading Local Gameplay Scene...");
        SceneManager.LoadScene("Main"); 
    }
}