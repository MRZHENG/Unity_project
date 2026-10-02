using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TowerDefenseWaveSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private TowerDefensePath path;
    [SerializeField] private List<WaveDefinition> waveDefinitions = new();
    [SerializeField] private Transform enemyRoot;

    private readonly Queue<WaveDefinition> queuedPlans = new();
    private TowerDefenseGameManager gameManager;
    private int currentWaveIndex;
    private int activeEnemyCount;
    private bool isSpawning;

    public bool IsWaveRunning => isSpawning || activeEnemyCount > 0;
    public int CurrentWaveNumber => Mathf.Min(currentWaveIndex, TotalWaveCount);
    public int TotalWaveCount => waveDefinitions.Count;

    private void Awake()
    {
        if (waveDefinitions.Count == 0)
        {
            waveDefinitions = TowerDefenseBalance.CreateDefaultWaves();
        }

        if (path == null)
        {
            path = FindObjectOfType<TowerDefensePath>();
        }

        if (spawnPoint == null)
        {
            GameObject spawn = GameObject.Find("SpawnPoint");
            if (spawn != null)
            {
                spawnPoint = spawn.transform;
            }
        }

        if (enemyRoot == null)
        {
            enemyRoot = new GameObject("RuntimeEnemies").transform;
        }
    }

    public void Initialize(TowerDefenseGameManager manager)
    {
        gameManager = manager;
    }

    public bool CanStartNextWave()
    {
        return !IsWaveRunning && currentWaveIndex < TotalWaveCount;
    }

    public bool StartNextWave()
    {
        if (!CanStartNextWave() || gameManager == null || path == null || path.Waypoints.Count == 0)
        {
            return false;
        }

        bool usingQueuedPlan = queuedPlans.Count > 0;
        WaveDefinition wave = usingQueuedPlan ? queuedPlans.Dequeue() : waveDefinitions[currentWaveIndex];
        currentWaveIndex++;
        StartCoroutine(SpawnWaveCoroutine(wave, currentWaveIndex));
        return true;
    }

    public void QueuePlannedWave(WaveDefinition wave)
    {
        if (wave == null)
        {
            return;
        }

        queuedPlans.Enqueue(wave);
    }

    public void NotifyEnemyRemoved(TowerDefenseEnemy enemy)
    {
        activeEnemyCount = Mathf.Max(0, activeEnemyCount - 1);
        if (!IsWaveRunning && activeEnemyCount == 0)
        {
            CompleteCurrentWave();
        }
    }

    private IEnumerator SpawnWaveCoroutine(WaveDefinition wave, int waveNumber)
    {
        isSpawning = true;
        int enemiesSent = 0;
        foreach (EnemySpawnInstruction instruction in wave.spawns)
        {
            enemiesSent += Mathf.Max(0, instruction.count);
        }

        gameManager.OnWaveStarted(waveNumber, wave.strategySummary, enemiesSent);

        foreach (EnemySpawnInstruction instruction in wave.spawns)
        {
            for (int index = 0; index < instruction.count; index++)
            {
                SpawnEnemy(instruction.enemyType);
                yield return new WaitForSeconds(instruction.interval);
            }
        }

        isSpawning = false;
        if (activeEnemyCount == 0)
        {
            CompleteCurrentWave();
        }
    }

    private void SpawnEnemy(EnemyType enemyType)
    {
        if (spawnPoint == null)
        {
            return;
        }

        GameObject enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemyObject.name = $"{enemyType}Enemy";
        enemyObject.transform.SetParent(enemyRoot, true);
        enemyObject.transform.position = spawnPoint.position + Vector3.up * 0.55f;

        TowerDefenseEnemy enemy = enemyObject.AddComponent<TowerDefenseEnemy>();
        enemy.Initialize(gameManager, this, path.Waypoints, enemyType);
        activeEnemyCount++;
    }

    private void CompleteCurrentWave()
    {
        if (currentWaveIndex <= 0 || currentWaveIndex > waveDefinitions.Count)
        {
            return;
        }

        WaveDefinition finishedWave = waveDefinitions[currentWaveIndex - 1];
        gameManager.OnWaveCompleted(finishedWave.completionReward);
    }
}
