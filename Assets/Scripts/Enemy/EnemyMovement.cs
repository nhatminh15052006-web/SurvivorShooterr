using UnityEngine;
using UnityEngine.AI;

public class EnemyMovement : MonoBehaviour
{
    Transform player;
    PlayerHealth playerHealth;
    EnemyHealth enemyHealth;
    NavMeshAgent nav;

    void Awake()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        player = playerObj.transform;
        playerHealth = playerObj.GetComponent<PlayerHealth>();
        enemyHealth = GetComponent<EnemyHealth>();
        nav = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (!nav.enabled) return;

        if (enemyHealth.currentHealth > 0 && playerHealth.currentHealth > 0)
        {
            if (nav.isOnNavMesh)
                nav.SetDestination(player.position);
        }
        else
        {
            nav.enabled = false;
        }
    }
}
