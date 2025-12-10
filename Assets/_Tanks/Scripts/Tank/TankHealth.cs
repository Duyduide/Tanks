using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankHealth : NetworkBehaviour
    {
        public float m_StartingHealth = 100f;               // The amount of health each tank starts with.
        public Slider m_Slider;                             // The slider to represent how much health the tank currently has.
        public Image m_FillImage;                           // The image component of the slider.
        public Color m_FullHealthColor = Color.green;    // The color the health bar will be when on full health.
        public Color m_ZeroHealthColor = Color.red;      // The color the health bar will be when on no health.
        public GameObject m_ExplosionPrefab;                // A prefab that will be instantiated in Awake, then used whenever the tank dies.
        [HideInInspector] public bool m_HasShield;          // Has the tank picked up a shield power up?
        
        // Network Variables
        public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        
        private AudioSource m_ExplosionAudio;               // The audio source to play when the tank explodes.
        private ParticleSystem m_ExplosionParticles;        // The particle system the will play when the tank is destroyed.
        private bool m_Dead;                                // Has the tank been reduced beyond zero health yet?
        private float m_ShieldValue;                        // Percentage of reduced damage when the tank has a shield.
        private bool m_IsInvincible;                        // Is the tank invincible in this moment?

        private void Awake ()
        {
            // Instantiate the explosion prefab and get a reference to the particle system on it.
            m_ExplosionParticles = Instantiate (m_ExplosionPrefab).GetComponent<ParticleSystem> ();

            // Get a reference to the audio source on the instantiated prefab.
            m_ExplosionAudio = m_ExplosionParticles.GetComponent<AudioSource> ();

            // Disable the prefab so it can be activated when it's required.
            m_ExplosionParticles.gameObject.SetActive (false);
            
            // Set the slider max value to the max health the tank can have
            m_Slider.maxValue = m_StartingHealth;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if(m_ExplosionParticles != null)
                Destroy(m_ExplosionParticles.gameObject);
        }

        private void OnEnable()
        {
            // When the tank is enabled, reset the tank's health and whether or not it's dead.
            if (IsServer)
            {
                CurrentHealth.Value = m_StartingHealth;
            }
            m_Dead = false;
            m_HasShield = false;
            m_ShieldValue = 0;
            m_IsInvincible = false;

            // Update the health slider's value and color.
            SetHealthUI();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            CurrentHealth.OnValueChanged += OnHealthChanged;
            SetHealthUI();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            CurrentHealth.OnValueChanged -= OnHealthChanged;
        }

        private void OnHealthChanged(float oldValue, float newValue)
        {
            SetHealthUI();
            
            // Check if tank just died
            if (newValue <= 0f && oldValue > 0f && !m_Dead)
            {
                OnDeath();
            }
        }


        [Rpc(SendTo.Server)]
        public void TakeDamageServerRpc(float amount)
        {
            // Check if the tank is not invincible and not already dead
            if (!m_IsInvincible && CurrentHealth.Value > 0f)
            {
                // Reduce current health by the amount of damage done.
                float damageTaken = amount * (1f - m_ShieldValue);
                CurrentHealth.Value = Mathf.Max(0f, CurrentHealth.Value - damageTaken);
            }
        }


        [Rpc(SendTo.Server)]
        public void IncreaseHealthServerRpc(float amount)
        {
            // Check if adding the amount would keep the health within the maximum limit
            if (CurrentHealth.Value + amount <= m_StartingHealth)
            {
                // If the new health value is within the limit, add the amount
                CurrentHealth.Value += amount;
            }
            else
            {
                // If the new health exceeds the starting health, set it at the maximum
                CurrentHealth.Value = m_StartingHealth;
            }
        }


        public void ToggleShield (float shieldAmount)
        {
            // Inverts the value of has shield.
            m_HasShield = !m_HasShield;

            // Stablish the amount of damage that will be reduced by the shield
            if (m_HasShield)
            {
                m_ShieldValue = shieldAmount;
            }
            else
            {
                m_ShieldValue = 0;
            }
        }

        public void ToggleInvincibility()
        {
            m_IsInvincible = !m_IsInvincible;
        }


        private void SetHealthUI ()
        {
            // Set the slider's value appropriately.
            m_Slider.value = CurrentHealth.Value;

            // Interpolate the color of the bar between the choosen colours based on the current percentage of the starting health.
            m_FillImage.color = Color.Lerp (m_ZeroHealthColor, m_FullHealthColor, CurrentHealth.Value / m_StartingHealth);
        }


        private void OnDeath ()
        {
            // Set the flag so that this function is only called once.
            m_Dead = true;

            // Check if explosion particles still exist before accessing them
            if (m_ExplosionParticles != null)
            {
                // Move the instantiated explosion prefab to the tank's position and turn it on.
                m_ExplosionParticles.transform.position = transform.position;
                m_ExplosionParticles.gameObject.SetActive (true);

                // Play the particle system of the tank exploding.
                m_ExplosionParticles.Play ();

                // Play the tank explosion sound effect.
                if (m_ExplosionAudio != null)
                {
                    m_ExplosionAudio.Play();
                }
            }

            // Turn the tank off.
            gameObject.SetActive (false);
        }
    }
}