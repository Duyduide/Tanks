Tuyệt vời\! Dưới đây là file Markdown chi tiết, bao gồm tất cả các script và hướng dẫn mạng cần thiết để bạn đưa cho GitHub Copilot Agent xử lý.

Tập trung vào việc chỉnh sửa các script **`TankMovement`**, **`TankHealth`**, **`TankShooting`**, và **`PowerUpDetector`** để hoạt động trong môi trường Netcode.

-----

# 🤖 Hướng dẫn Chuyển đổi Prefab Tank sang Netcode 1v1

## 🎯 Mục tiêu (Goal)

Tích hợp chức năng mạng (Netcode for GameObjects - NGO) vào Prefab **`Tank - Medium Variant`** và các script đính kèm để hỗ trợ gameplay Multiplayer 1v1.

### ⚠️ Yêu cầu Bắt buộc (Prerequisites)

1.  Prefab **`Tank - Medium Variant`** phải có **`NetworkObject`** và **`NetworkTransform`**.
2.  Prefab **`CompleteShell`** phải có **`NetworkObject`** và **`NetworkTransform`**, và đã được đăng ký trong NetworkManager.
3.  Scene **`Network`** đã xóa/vô hiệu hóa các đối tượng UI, Tank và `GameManager.cs` cũ.

-----

## 📝 Các Bước Chỉnh sửa Script Cụ thể

### 1\. `TankHealth.cs` (Máu và Sát thương)

  * **Đồng bộ hóa Máu:** Thay đổi biến máu local thành `NetworkVariable`.
  * **Xử lý Sát thương:** Chỉ Server/Host được thay đổi máu và xử lý cái chết.

<!-- end list -->

```csharp
// File: TankHealth.cs

// 1. Thay đổi Biến Máu
// Xóa: private float m_CurrentHealth;
// Thêm:
public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(
    new NetworkVariableSettings { WritePermission = NetworkVariablePermission.ServerOnly }, 
    100f
);
// Thêm: private bool m_Dead; (Nếu muốn dùng m_Dead để xử lý UI/logic cục bộ)

// 2. Hàm OnEnable() - Cần reset máu trên Server
private void OnEnable()
{
    // Cần thay thế logic reset máu cục bộ bằng NetworkVariable
    if (IsServer)
    {
        CurrentHealth.Value = m_StartingHealth;
        // ...
    }
    // ...
}

// 3. Xử lý Sát thương (Chỉ Server/Host)
// Thay thế public void TakeDamage (float amount) bằng ServerRpc
[ServerRpc(RequireOwnership = false)]
public void TakeDamageServerRpc (float amount)
{
    if (CurrentHealth.Value <= 0f) return;
    
    // Giữ logic tính toán sát thương (Shield và Invincibility nên là NetworkVariable nếu cần đồng bộ chính xác)
    float damageTaken = amount * (1f - m_ShieldValue);
    
    // Thay đổi NetworkVariable
    CurrentHealth.Value -= damageTaken;
}

// 4. Đồng bộ hóa UI và Cái chết (Client/Everyone)
public override void OnNetworkSpawn()
{
    CurrentHealth.OnValueChanged += OnHealthChanged;
    // ...
}

private void OnHealthChanged(float oldValue, float newValue)
{
    SetHealthUI();
    // Logic OnDeath (Xử lý hiệu ứng nổ và ẩn Tank)
    if (newValue <= 0f && oldValue > 0f)
    {
        // OnDeath() hiện tại dùng gameObject.SetActive(false); => Cần xem xét NetworkObject.Despawn() trên Host
        OnDeath(); 
    }
}
```

-----

### 2\. `TankMovement.cs` (Di chuyển)

  * **Quyền Sở hữu:** Chỉ Owner được đọc Input.
  * **Lực Nổ:** Đồng bộ hóa lực đẩy từ vụ nổ.

<!-- end list -->

```csharp
// File: TankMovement.cs (Đã kế thừa NetworkBehaviour)

// 1. Thêm Quyền Sở hữu (Khuyên dùng: tắt script nếu không phải Owner)
public override void OnNetworkSpawn()
{
    // Nếu không phải là chủ sở hữu, tắt script để ngăn việc đọc Input local
    if (!IsOwner)
    {
        enabled = false;
    }
    base.OnNetworkSpawn();
}

// 2. Đồng bộ Lực Nổ (AddExplosionForce)
// Bổ sung hàm để ShellExplosion gọi lên Server
[ServerRpc(RequireOwnership = false)]
private void AddExplosionForceServerRpc(float explosionForce, Vector3 explosionPosition, float explosionRadius, float upwardsModifier = 0f)
{
    // Hàm này chạy trên Host/Server
    AddExplosionForce(explosionForce, explosionPosition, explosionRadius, upwardsModifier);
    // NetworkTransform sẽ đồng bộ vị trí bị đẩy lùi cho Clients.
}

// 3. Xử lý Input (Đã có sẵn trong Update() nhưng cần đảm bảo chỉ chạy nếu IsOwner)
// Hàm Update() phải đảm bảo: if (IsOwner && !m_IsComputerControlled) { /* read input */ }
// Vì bạn đã tắt script nếu không phải Owner trong OnNetworkSpawn, logic này sẽ được bảo vệ.
```

-----

### 3\. `TankShooting.cs` (Bắn đạn)

  * **Bắn đạn:** Dùng `ServerRpc` để sinh ra đạn mạng.

<!-- end list -->

```csharp
// File: TankShooting.cs (Đã kế thừa NetworkBehaviour)

// 1. Chuyển logic Fire() thành ServerRpc
private void Fire ()
{
    // 1. [OWNER ONLY] Set the fired flag, reset UI/Local state, Play Charging/Fire Audio...
    // ...
    
    // Gửi yêu cầu lên Server/Host để sinh đạn
    FireServerRpc(m_CurrentLaunchForce, m_HasSpecialShell, m_SpecialShellMultiplier);

    // [CLIENT-SIDE] Chỉ chạy hiệu ứng âm thanh/UI cục bộ:
    m_ShootingAudio.clip = m_FireClip;
    m_ShootingAudio.Play ();
    
    // ...
}


[ServerRpc]
private void FireServerRpc(float launchForce, bool hasSpecialShell, float specialMultiplier)
{
    // 1. Instantiate shell trên Server
    Rigidbody shellInstance =
        Instantiate (m_Shell, m_FireTransform.position, m_FireTransform.rotation) as Rigidbody;

    // 2. Spawn shell qua mạng (Server sở hữu)
    shellInstance.GetComponent<NetworkObject>().Spawn(); 
    
    // 3. Áp dụng vật lý (Chỉ Server/Host xử lý)
    shellInstance.linearVelocity = launchForce * m_FireTransform.forward;
    
    // 4. Cấu hình đạn
    ShellExplosion explosionData = shellInstance.GetComponent<ShellExplosion>();
    explosionData.m_ExplosionForce = m_ExplosionForce;
    explosionData.m_ExplosionRadius = m_ExplosionRadius;
    explosionData.m_MaxDamage = m_MaxDamage;
    
    // 5. Xử lý Special Shell và reset trạng thái (Cần đồng bộ hóa qua mạng)
    if (hasSpecialShell)
    {
        explosionData.m_MaxDamage *= specialMultiplier;
        // BẮT BUỘC: Cần gọi ClientRpc để reset trạng thái Special Shell trên tất cả Clients
        ResetSpecialShellClientRpc(); 
    }
}

// 2. ClientRpc để reset trạng thái Special Shell trên Clients
[ClientRpc]
private void ResetSpecialShellClientRpc()
{
    // Logic reset Special Shell (chạy trên tất cả Clients)
    m_HasSpecialShell = false;
    m_SpecialShellMultiplier = 1f;
    
    // Reset HUD
    PowerUpDetector powerUpDetector = GetComponent<PowerUpDetector>();
    if (powerUpDetector != null)
    {
        powerUpDetector.m_HasActivePowerUp = false;
    }

    PowerUpHUD powerUpHUD = GetComponentInChildren<PowerUpHUD>();
    if (powerUpHUD != null)
        powerUpHUD.DisableActiveHUD();
}
```

-----

### 4\. `PowerUpDetector.cs` (Power-Up)

  * **Kế thừa:** Thay thế `MonoBehaviour` bằng **`NetworkBehaviour`**.
  * **Logic:** Mọi hàm Power-Up (như `PowerUpSpeed`, `PickUpShield`, v.v.) phải được kích hoạt bằng **`ServerRpc`** (nếu Client nhặt) và đồng bộ hóa hiệu ứng bằng **`ClientRpc`**.

<!-- end list -->

```csharp
// File: PowerUpDetector.cs

// 1. Thay đổi Kế thừa
using Unity.Netcode;
public class PowerUpDetector : NetworkBehaviour
{
    // ...

    // 2. Ví dụ: PowerUpSpeed
    public void PowerUpSpeed(float speedBoost, float turnSpeedBoost, float duration)
    {
        if (IsOwner)
        {
            // Gửi yêu cầu lên Server để kích hoạt và hẹn giờ
            ActivateSpeedServerRpc(speedBoost, turnSpeedBoost, duration);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void ActivateSpeedServerRpc(float speedBoost, float turnSpeedBoost, float duration)
    {
        // [HOST LOGIC] Áp dụng hiệu ứng và bắt đầu hẹn giờ
        StartCoroutine(IncreaseSpeed(speedBoost, turnSpeedBoost, duration));
        
        // [Đồng bộ hóa] Gửi lệnh đến tất cả Clients để cập nhật HUD
        SpeedBoostClientRpc(duration);
    }
    
    [ClientRpc]
    private void SpeedBoostClientRpc(float duration)
    {
        // [CLIENT LOGIC] Cập nhật HUD trên Client
        m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.Speed, duration);
    }
    
    // **Quan trọng:** Coroutine IncreaseSpeed(float speedBoost, float TurnSpeedBoost, float duration)
    // PHẢI CHỈ CHẠY TRÊN HOST/SERVER để thay đổi m_TankMovement.m_Speed (vì m_Speed không phải NetworkVariable)
    private IEnumerator IncreaseSpeed(float speedBoost, float TurnSpeedBoost, float duration)
    {
        if (!IsServer) yield break; // Chỉ Host mới xử lý logic này

        m_TankMovement.m_Speed += speedBoost;
        m_TankMovement.m_TurnSpeed += TurnSpeedBoost;
        
        yield return new WaitForSeconds(duration);
        
        m_TankMovement.m_Speed -= speedBoost;
        m_TankMovement.m_TurnSpeed -= TurnSpeedBoost;
    }
    // ... (Áp dụng logic tương tự cho các Power-Up khác: Shield, Healing, Invincibility)
}
```