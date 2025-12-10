// File: LobbyManager.cs
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine.SceneManagement;
using Unity.Netcode.Transports.UTP;

public class LobbyManager : MonoBehaviour
{
    private Lobby joinedLobby;
    private const int MaxPlayers = 2; // Giới hạn 1vs1
    private const string RelayJoinCodeKey = "JoinCode";
    private Coroutine lobbyHeartbeatCoroutine;

    [SerializeField]
    private string multiplayerGameSceneName = "Network";

    [SerializeField]
    private string connectionType = "udp"; // "dtls" hoặc "udp"

    [SerializeField] private MainMenuController menuController;

    public async void QuickJoinOrCreateLobby()
    {
        Debug.Log("Bắt đầu Quick Join hoặc tạo phòng...");
        
        // 1. Khởi tạo Unity Services (Nếu chưa)
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await InitializeUnityServices();
        }

        try
        {
            // 2. Thử Quick Join
            joinedLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            
            // Nếu thành công, tham gia với tư cách Client
            await JoinRelayAsClient(joinedLobby);

            Debug.Log($"Đã tham gia Lobby: {joinedLobby.Name}");
        }
        catch (LobbyServiceException)
        {
            // 3. Nếu Quick Join thất bại (không có phòng), tạo phòng mới (Host)
            Debug.LogWarning("Không tìm thấy phòng có sẵn. Tạo phòng mới...");
            await CreateLobbyAsHost();
        }
    }

    // --- CÁC HÀM HỖ TRỢ ---

    private async Task InitializeUnityServices()
    {
        await UnityServices.InitializeAsync();
        
        // Đăng nhập vô danh (cho mục đích test đơn giản)
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        Debug.Log("Unity Services và Authentication đã sẵn sàng.");
    }

    private async Task CreateLobbyAsHost()
    {
        try
        {
            // Hiển thị màn hình chờ nếu có MenuController
            if (menuController != null)
            {
                menuController.ShowWaitingScreen();
            }

            string lobbyName = "TankArena_" + Random.Range(1000, 9999);
            
            // 1. Start Host với Relay và lấy Join Code
            string relayJoinCode = await StartHostWithRelay(MaxPlayers - 1, connectionType);
            
            if (string.IsNullOrEmpty(relayJoinCode))
            {
                Debug.LogError("Không thể tạo Host với Relay!");
                return;
            }
            
            // 2. Tạo Lobby với Join Code
            CreateLobbyOptions options = new CreateLobbyOptions 
            {
                IsPrivate = false, // Cho phép tìm kiếm công khai
                Data = new System.Collections.Generic.Dictionary<string, DataObject> 
                {
                    { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode) }
                }
            };

            joinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, MaxPlayers, options);
            Debug.Log($"Tạo Lobby thành công: {joinedLobby.Name} | ID: {joinedLobby.Id} | Join Code: {relayJoinCode}");

            // 3. Bắt đầu Heartbeat để giữ Lobby hoạt động
            StartLobbyHeartbeat();

            // 4. Đăng ký lắng nghe sự kiện client kết nối để load scene khi đủ người
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedToHost;
            
            Debug.Log($"[LobbyManager] Host đang chờ client kết nối... (Hiện tại: {NetworkManager.Singleton.ConnectedClients.Count}/{MaxPlayers})");
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Lỗi Relay khi tạo Host: " + e.Message);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Lỗi Lobby khi tạo Host: " + e.Message);
        }
    }

    private async Task JoinRelayAsClient(Lobby lobby)
    {
        try
        {
            // 1. Lấy Relay Join Code từ Lobby Data
            if (lobby.Data == null || !lobby.Data.ContainsKey(RelayJoinCodeKey))
            {
                Debug.LogError("Lobby không có Relay Join Code!");
                return;
            }

            string relayJoinCode = lobby.Data[RelayJoinCodeKey].Value;
            
            // 2. Start Client với Relay
            bool success = await StartClientWithRelay(relayJoinCode, connectionType);
            
            if (success)
            {
                Debug.Log("Client đã kết nối Netcode thành công.");
            }
            else
            {
                Debug.LogError("Không thể kết nối với Host!");
            }

            // Client không cần Heartbeat
            if (lobbyHeartbeatCoroutine != null)
            {
                StopCoroutine(lobbyHeartbeatCoroutine);
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Lỗi Relay khi Client Join: " + e.Message);
        }
    }

    private void StartLobbyHeartbeat()
    {
        if (lobbyHeartbeatCoroutine != null)
        {
            StopCoroutine(lobbyHeartbeatCoroutine);
        }
        lobbyHeartbeatCoroutine = StartCoroutine(LobbyHeartbeatCoroutine());
    }

    private IEnumerator LobbyHeartbeatCoroutine()
    {
        var wait = new WaitForSeconds(15f);
        while (joinedLobby != null)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
            yield return wait;
        }
    }

    private void OnClientConnectedToHost(ulong clientId)
    {
        // Chỉ Host mới xử lý logic này
        if (!NetworkManager.Singleton.IsServer)
            return;

        int connectedCount = NetworkManager.Singleton.ConnectedClients.Count;
        Debug.Log($"[LobbyManager] Client {clientId} kết nối! Tổng: {connectedCount}/{MaxPlayers}");

        // Khi đủ 2 người chơi (1vs1), load scene game
        if (connectedCount >= MaxPlayers)
        {
            Debug.Log($"[LobbyManager] Đủ {MaxPlayers} người chơi! Bắt đầu load scene game...");
            
            // Kiểm tra scene có trong Build Settings
            if (!IsSceneInBuildSettings(multiplayerGameSceneName))
            {
                Debug.LogError($"[LobbyManager] Scene '{multiplayerGameSceneName}' KHÔNG có trong Build Settings! Vui lòng thêm vào File > Build Settings.");
                return;
            }
            
            // Hủy đăng ký callback để tránh gọi lại
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToHost;
            
            // Load scene game
            NetworkManager.Singleton.SceneManager.LoadScene(multiplayerGameSceneName, LoadSceneMode.Single);
        }
    }

    private bool IsSceneInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameInBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (sceneNameInBuild == sceneName)
                return true;
        }
        return false;
    }

    // --- RELAY HELPER FUNCTIONS ---

    public async Task<string> StartHostWithRelay(int maxConnections, string connectionType)
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        
        // Kiểm tra NetworkManager tồn tại
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is null! Đảm bảo có NetworkManager trong scene.");
            return null;
        }
        
        var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport component không tìm thấy trên NetworkManager!");
            return null;
        }
        
        transport.SetRelayServerData(
            AllocationUtils.ToRelayServerData(allocation, connectionType)
        );
        
        var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        return NetworkManager.Singleton.StartHost() ? joinCode : null;
    }

    public async Task<bool> StartClientWithRelay(string joinCode, string connectionType)
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        
        // Kiểm tra NetworkManager tồn tại
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is null! Đảm bảo có NetworkManager trong scene.");
            return false;
        }

        var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode: joinCode);
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport component không tìm thấy trên NetworkManager!");
            return false;
        }
        
        transport.SetRelayServerData(
            AllocationUtils.ToRelayServerData(allocation, connectionType)
        );
        
        return !string.IsNullOrEmpty(joinCode) && NetworkManager.Singleton.StartClient();
    }

    private void OnDestroy()
    {
        if (lobbyHeartbeatCoroutine != null)
        {
            StopCoroutine(lobbyHeartbeatCoroutine);
        }
        
        // Hủy đăng ký callback nếu còn
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedToHost;
        }
    }
}