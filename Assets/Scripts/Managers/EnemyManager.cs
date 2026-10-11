using System.Collections;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public PlayerHealth playerHealth;
    public GameObject enemy;
    public float spawnTime = 3f;        // khoảng cách giữa 2 lần spawn lúc đầu
    public float minSpawnTime = 1.2f;   // khoảng cách nhỏ nhất (độ khó tối đa)
    public float rampPerSpawn = 0.05f;  // mỗi lần spawn, rút ngắn khoảng cách đi chừng này
    public Transform[] spawnPoints;

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        float interval = spawnTime;
        yield return new WaitForSeconds(interval);

        while (playerHealth.currentHealth > 0)
        {
            Spawn();
            interval = Mathf.Max(minSpawnTime, interval - rampPerSpawn);
            yield return new WaitForSeconds(interval);
        }
    }

    void Spawn()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        int i = Random.Range(0, spawnPoints.Length);
        Instantiate(enemy, spawnPoints[i].position, spawnPoints[i].rotation);
    }
}
