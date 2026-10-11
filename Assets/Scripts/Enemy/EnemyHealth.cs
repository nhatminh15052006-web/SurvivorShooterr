using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    public int startingHealth = 100;
    public int currentHealth;
    public float sinkSpeed = 2.5f;
    public float sinkDelay = 1.5f;      // thời gian chờ sau khi chết rồi mới chìm xuống đất
    public int scoreValue = 10;
    public AudioClip deathClip;

    Animator anim;
    AudioSource enemyAudio;
    ParticleSystem hitParticles;
    CapsuleCollider capsuleCollider;
    bool isDead;
    bool isSinking;

    void Awake()
    {
        anim = GetComponent<Animator>();
        enemyAudio = GetComponent<AudioSource>();
        hitParticles = GetComponentInChildren<ParticleSystem>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        currentHealth = startingHealth;
    }

    void Update()
    {
        if (isSinking)
            transform.Translate(-Vector3.up * sinkSpeed * Time.deltaTime);
    }

    public void TakeDamage(int amount, Vector3 hitPoint)
    {
        if (isDead) return;

        if (enemyAudio != null) enemyAudio.Play();

        currentHealth -= amount;

        if (hitParticles != null)
        {
            hitParticles.transform.position = hitPoint;
            hitParticles.Play();
        }

        if (currentHealth <= 0)
            Death();
    }

    void Death()
    {
        isDead = true;

        capsuleCollider.isTrigger = true;
        gameObject.layer = 0;   // xác chết không còn chặn đạn

        anim.SetTrigger("Dead");

        if (enemyAudio != null)
        {
            enemyAudio.clip = deathClip;
            enemyAudio.Play();
        }

        ScoreManager.score += scoreValue;

        Invoke("StartSinking", sinkDelay);
    }

    public void StartSinking()
    {
        if (isSinking) return;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        isSinking = true;
        Destroy(gameObject, 2f);
    }
}
