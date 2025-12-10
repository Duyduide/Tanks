// File: MainMenuController.cs
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject initialButtonsPanel; // (Gán cho InitialPanel)
    [SerializeField] private GameObject multiplayerMenuPanel; // (Gán cho MultiplayerPanel)
    [SerializeField] private GameObject waitingPanel;
    void Start()
    {
        // Khi Scene Menu khởi động:
        // 1. Luôn hiển thị Panel chính (Local & Network)
        initialButtonsPanel.SetActive(true);
        // 2. Luôn ẩn Panel Multiplayer (Create Room & Quick Join)
        multiplayerMenuPanel.SetActive(false); 
    }

    // Gắn vào nút "Local Gameplay"
    public void OnLocalGameplayClicked()
    {
        Debug.Log("Loading Local Gameplay Scene...");
        SceneManager.LoadScene("Main"); 
    }

    // Gắn vào nút "Network"
    public void OnMultiplayerClicked()
    {
        Debug.Log("Chuyển sang Menu Multiplayer...");
        
        // 1. Ẩn Panel chứa nút Local/Network
        initialButtonsPanel.SetActive(false);
        
        // 2. Hiện Panel chứa nút Create Room/Quick Join
        multiplayerMenuPanel.SetActive(true); 
    }

    // Gắn vào nút "Back" trên MultiplayerPanel (Nếu bạn thêm nút Back)
    public void OnBackClicked()
    {
        Debug.Log("Quay lại Menu Chính...");
        multiplayerMenuPanel.SetActive(false);
        initialButtonsPanel.SetActive(true);
    }

    public void ShowWaitingScreen() 
    {
        // Ẩn tất cả các panel khi chờ kết nối
        initialButtonsPanel.SetActive(false);
        multiplayerMenuPanel.SetActive(false);
        waitingPanel.SetActive(true);
    }
}