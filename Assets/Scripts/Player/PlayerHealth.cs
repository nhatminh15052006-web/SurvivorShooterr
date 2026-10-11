using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public int startingHealth = 100;
    public int currentHealth;
    public Slider healthSlider;
    public Image damageImage;
    public AudioClip deathClip;
    public float flashSpeed = 5f;
    public Color flashColour = new Color(1f, 0f, 0f, 0.1f);

    Animator anim;
    AudioSource playerAudio;
    PlayerMovement playerMovement;
    PlayerShooting playerShooting;
    bool isDead;
    bool damaged;

    void Awake()
    {
        anim = GetComponent<Animator>();
        playerAudio = GetComponent<AudioSource>();
        playerMovement = GetComponent<PlayerMovement>();
        playerShooting = GetComponentInChildren<PlayerShooting>();
        currentHealth = startingHealth;
    }

    void Start()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = startingHealth;
            healthSlider.value = currentHealth;
        }
    }

    void Update()
    {
        if (damageImage != null)
        {
            if (damaged)
                damageImage.color = flashColour;
            else
                damageImage.color = Color.Lerp(damageImage.color, Color.clear, flashSpeed * Time.deltaTime);
        }
        damaged = false;
    }

    public void TakeDamage(int amount)
    {
        if (isDead) return;

        damaged = true;
        currentHealth -= amount;

        if (healthSlider != null)
            healthSlider.value = Mathf.Max(0, currentHealth);

        if (playerAudio != null)
            playerAudio.Play();

        if (currentHealth <= 0)
            Death();
    }

    void Death()
    {
        isDead = true;

        if (playerShooting != null)
        {
            playerShooting.DisableEffects();
            playerShooting.enabled = false;
        }

        anim.SetTrigger("Die");

        if (playerAudio != null)
        {
            playerAudio.clip = deathClip;
            playerAudio.Play();
        }

        playerMovement.enabled = false;
    }

    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
