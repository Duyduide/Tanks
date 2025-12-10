// File: NetworkGameManager.cs
using Unity.Netcode;
using UnityEngine;
using System.Collections;
using Tanks.Complete;

public class NetworkGameManager : NetworkBehaviour
{
    // Cấu hình mạng và đối tượng
    [Header("Network Prefabs")]
    [SerializeField] private GameObject m_TankMediumPrefab; // Prefab Tank Medium (có NetworkObject)
    
    // Sử dụng cấu trúc TankManager để lấy Spawn Point
    [Header("Spawn Configuration")]
    [Tooltip("Dùng để gán các điểm Spawn Transform - Kéo SpawnPoint1 và SpawnPoint2 vào đây")]
    [SerializeField] private Transform[] m_SpawnTransforms = new Transform[2];
    
    private TankManager[] m_SpawnPoints; // Được khởi tạo từ m_SpawnTransforms
    
    // Tham chiếu đến Camera Control
    [Header("Game References")]
    [SerializeField] private CameraControl m_CameraControl; 
    
    private int m_PlayersSpawned = 0; // Đếm số người chơi đã spawn
    private const int MaxPlayers = 2; // Giới hạn 1vs1
    
    // --- KHỞI TẠO VÀ SPAWN ---

    // public override void OnNetworkSpawn()
    // {
    //     if (!IsHost) return; // Chỉ Host mới thực hiện logic quản lý

    //     // Khởi tạo TankManager[] từ Transform[] đã gán trong Inspector
    //     InitializeSpawnPoints();

    //     // 1. Đăng ký sự kiện kết nối của Client
    //     NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        
    //     // 2. Spawn xe tăng cho Host
    //     SpawnTank(NetworkManager.Singleton.LocalClientId, 0); 
    // }

    // 10/12/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

    public override void OnNetworkSpawn()
    {
        if (!IsHost) return; // Only Host executes spawn logic

        // Ensure we are in the correct scene
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Network")
        {
            return;
        }

        // Initialize spawn points
        InitializeSpawnPoints();

        // Register client connection event
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;

        // Spawn tank for Host
        SpawnTank(NetworkManager.Singleton.LocalClientId, 0);
    }

    private void InitializeSpawnPoints()
    {
        // Tạo mảng TankManager từ các Transform đã gán
        m_SpawnPoints = new TankManager[m_SpawnTransforms.Length];
        
        for (int i = 0; i < m_SpawnTransforms.Length; i++)
        {
            m_SpawnPoints[i] = new TankManager();
            m_SpawnPoints[i].m_SpawnPoint = m_SpawnTransforms[i];
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        // Khi một Client kết nối (thường là người thứ 2)
        if (NetworkManager.Singleton.ConnectedClients.Count == MaxPlayers)
        {
            // Spawn tank cho Client đó (Spawn Point index 1)
            SpawnTank(clientId, 1);
            
            // Thiết lập camera sau khi đã spawn đủ 2 tank
            SetupCameraTargets(); 
            
            // Bắt đầu vòng lặp game (GameLoop đơn giản cho mạng)
            StartCoroutine(StartGameLoop()); 

            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void SpawnTank(ulong clientId, int spawnIndex)
    {
        if (spawnIndex >= m_SpawnPoints.Length) return;

        Transform spawnPoint = m_SpawnPoints[spawnIndex].m_SpawnPoint;

        // 1. Instantiate trên Server/Host
        GameObject playerTank = Instantiate(
            m_TankMediumPrefab, 
            spawnPoint.position, 
            spawnPoint.rotation
        );
        
        // 2. Gán màu sắc (Bạn cần tự đồng bộ màu sắc qua NetworkVariable trên Prefab Tank)
        Color tankColor = (spawnIndex == 0) ? Color.red : Color.blue;

        // 3. Spawn và Giao quyền sở hữu
        NetworkObject networkObject = playerTank.GetComponent<NetworkObject>();
        networkObject.SpawnAsPlayerObject(clientId, true); 
        
        m_PlayersSpawned++;
        
        // Cấu hình TankManager (chỉ chạy trên Server/Host)
        // Đây là bước quan trọng để Setup các script Movement/Shooting
        // Tùy biến TankManager:
        m_SpawnPoints[spawnIndex].m_Instance = playerTank;
        m_SpawnPoints[spawnIndex].m_PlayerNumber = spawnIndex + 1;
        m_SpawnPoints[spawnIndex].m_PlayerColor = tankColor;
        m_SpawnPoints[spawnIndex].Setup(null); // Có thể cần thay đổi hàm Setup trong TankManager.cs
    }
    
    // --- THIẾT LẬP CAMERA ---
    
    private void SetupCameraTargets()
    {
        if (!IsHost) return; // Chỉ Host thiết lập camera (vì m_SpawnPoints chỉ có Instance trên Host)

        // Create a collection of transforms the same size as the number of tanks.
        Transform[] targets = new Transform[MaxPlayers];
        
        // For each of these transforms...
        for (int i = 0; i < targets.Length; i++)
        {
            // ... set it to the appropriate tank transform.
            targets[i] = m_SpawnPoints[i].m_Instance.transform;
        }

        // These are the targets the camera should follow.
        m_CameraControl.m_Targets = targets;
        
        // Thiết lập vị trí và zoom ban đầu
        m_CameraControl.SetStartPositionAndSize();
    }
    
    // --- VÒNG LẶP GAME ĐơN GIẢN ---
    
    private IEnumerator StartGameLoop()
    {
        // Chờ 1 giây để đảm bảo mọi thứ đã khởi tạo trên Client
        yield return new WaitForSeconds(1.0f); 
        
        // Hiện tại không có logic vòng đấu (RoundStarting/RoundPlaying) nên ta chỉ cần đảm bảo xe tăng được kích hoạt
        
        // Kích hoạt điều khiển cho tất cả tank
        for (int i = 0; i < MaxPlayers; i++)
        {
            m_SpawnPoints[i].EnableControl();
        }
    }
    
    // --- DỌN DẸP ---
    
    public override void OnNetworkDespawn()
    {
        if (IsHost)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
        base.OnNetworkDespawn();
    }
}
