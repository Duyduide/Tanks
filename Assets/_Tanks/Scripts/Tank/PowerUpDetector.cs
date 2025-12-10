using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Tanks.Complete
{
    public class PowerUpDetector : NetworkBehaviour
    {
        // Variable that indicates if the tank has a PowerUp right now
        public bool m_HasActivePowerUp = false;
        // References to the tank's components
        private TankShooting m_TankShooting;
        private TankMovement m_TankMovement;
        private TankHealth m_TankHealth;
        private PowerUpHUD m_PowerUpHUD;

        private void Awake()
        {
            // Get references to the tank's movement, shooting, and health components
            m_TankShooting = GetComponent<TankShooting>();
            m_TankMovement = GetComponent<TankMovement>();
            m_TankHealth = GetComponent<TankHealth>();
            m_PowerUpHUD = GetComponentInChildren<PowerUpHUD>();
        }

        // Applies a temporary speed boost to the tank
        public void PowerUpSpeed(float speedBoost, float turnSpeedBoost, float duration)
        {
            if (IsOwner || IsServer)
            {
                ActivateSpeedServerRpc(speedBoost, turnSpeedBoost, duration);
            }
        }

        [Rpc(SendTo.Server)]
        private void ActivateSpeedServerRpc(float speedBoost, float turnSpeedBoost, float duration)
        {
            StartCoroutine(IncreaseSpeed(speedBoost, turnSpeedBoost, duration));
            SpeedBoostClientRpc(duration);
        }

        [Rpc(SendTo.Everyone)]
        private void SpeedBoostClientRpc(float duration)
        {
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.Speed, duration);
        }

        // Coroutine to temporarily increase the tank's movement speed and turn speed
        private IEnumerator IncreaseSpeed(float speedBoost, float TurnSpeedBoost, float duration)
        {
            if (!IsServer) yield break; // Chỉ Host mới xử lý logic này

            // Apply the speed boost
            m_HasActivePowerUp = true;
            m_TankMovement.m_Speed += speedBoost;
            m_TankMovement.m_TurnSpeed += TurnSpeedBoost;
            // Wait for the duration of the power up
            yield return new WaitForSeconds(duration);
            // Revert the speed boost 
            m_TankMovement.m_Speed -= speedBoost;
            m_TankMovement.m_TurnSpeed -= TurnSpeedBoost;
            m_HasActivePowerUp = false;
        }

        // Applies a temporary shooting rate boost to the tank
        public void PowerUpShoootingRate(float cooldownReduction, float duration)
        {
            if (IsOwner || IsServer)
            {
                ActivateShootingRateServerRpc(cooldownReduction, duration);
            }
        }

        [Rpc(SendTo.Server)]
        private void ActivateShootingRateServerRpc(float cooldownReduction, float duration)
        {
            StartCoroutine(IncreaseShootingRate(cooldownReduction, duration));
            ShootingRateClientRpc(duration);
        }

        [Rpc(SendTo.Everyone)]
        private void ShootingRateClientRpc(float duration)
        {
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.ShootingBonus, duration);
        }

        // Coroutine to temporarily enhance the tank's shooting rate
        private IEnumerator IncreaseShootingRate(float cooldownReduction, float duration)
        {
            if (!IsServer) yield break; // Chỉ Host mới xử lý logic này
            
            // Apply the shooting cooldown reduction if it is greater than zero
            if(cooldownReduction > 0)
            {
                m_HasActivePowerUp = true;
                m_TankShooting.m_ShotCooldown *= cooldownReduction;
                // Wait for the duration of the power up
                yield return new WaitForSeconds(duration);
                // Revert the shooting boost after the duration ends
                m_TankShooting.m_ShotCooldown /= cooldownReduction;
                m_HasActivePowerUp = false;
            }
        }

        // Grants the tank a temporary shield if it does not already have one
        public void PickUpShield(float shieldAmount, float duration)
        {
            if (!m_TankHealth.m_HasShield && (IsOwner || IsServer))
            {
                ActivateShieldServerRpc(shieldAmount, duration);
            }
        }

        [Rpc(SendTo.Server)]
        private void ActivateShieldServerRpc(float shieldAmount, float duration)
        {
            if (!m_TankHealth.m_HasShield)
            {
                StartCoroutine(ActivateShield(shieldAmount, duration));
                ShieldClientRpc(duration);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void ShieldClientRpc(float duration)
        {
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.DamageReduction, duration);
        }

        // Grants the tank a temporary shield if it does not already have one
        private IEnumerator ActivateShield(float shieldAmount, float duration)
        {
            if (!IsServer) yield break; // Chỉ Host mới xử lý logic này
            
            // Activate the shield
            m_HasActivePowerUp = true;
            m_TankHealth.ToggleShield(shieldAmount);
            // Wait for the duration of the power up
            yield return new WaitForSeconds(duration);
            // Deactivate the shield
            m_TankHealth.ToggleShield(shieldAmount);
            m_HasActivePowerUp = false;
        }

        // Increases the health of the tank
        public void PowerUpHealing(float healAmount)
        {
            if (IsOwner || IsServer)
            {
                m_TankHealth.IncreaseHealthServerRpc(healAmount);
                HealingClientRpc();
            }
        }

        [Rpc(SendTo.Everyone)]
        private void HealingClientRpc()
        {
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.Healing, 1.0f);
        }

        // Makes the tank invulnerable for an amount of time
        public void PowerUpInvincibility(float duration)
        {
            if (IsOwner || IsServer)
            {
                ActivateInvincibilityServerRpc(duration);
            }
        }

        [Rpc(SendTo.Server)]
        private void ActivateInvincibilityServerRpc(float duration)
        {
            StartCoroutine(ActivateInvincibility(duration));
            InvincibilityClientRpc(duration);
        }

        [Rpc(SendTo.Everyone)]
        private void InvincibilityClientRpc(float duration)
        {
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.Invincibility, duration);
        }

        private IEnumerator ActivateInvincibility(float duration)
        {
            if (!IsServer) yield break; // Chỉ Host mới xử lý logic này
            
            m_HasActivePowerUp = true;
            m_TankHealth.ToggleInvincibility();
            yield return new WaitForSeconds(duration);
            m_HasActivePowerUp = false;
            m_TankHealth.ToggleInvincibility();
        }

        // Equips the tank with a special shell that increases damage
        public void PowerUpSpecialShell(float damageMultiplier)
        {
            if (IsOwner || IsServer)
            {
                SpecialShellClientRpc(damageMultiplier);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void SpecialShellClientRpc(float damageMultiplier)
        {
            m_HasActivePowerUp = true;
            m_PowerUpHUD.SetActivePowerUp(PowerUp.PowerUpType.DamageMultiplier, 0f);
            m_TankShooting.EquipSpecialShell(damageMultiplier);
        }
    }
}
