using System.Collections.Generic;
using UnityEngine;

public class TowerDefenseEnemy : MonoBehaviour
{
    private static readonly List<TowerDefenseEnemy> ActiveEnemyList = new();

    [SerializeField] private EnemyType enemyType = EnemyType.Normal;

    private TowerDefenseGameManager gameManager;
    private TowerDefenseWaveSpawner waveSpawner;
    private IReadOnlyList<Transform> path;
    private int waypointIndex;
    private float currentHealth;
    private float currentMoveSpeed;
    private float slowUntilTime;
    private float slowMultiplier = 1f;

    public static IReadOnlyList<TowerDefenseEnemy> ActiveEnemies => ActiveEnemyList;
    public Vector3 Position => transform.position;
    public bool IsAlive => currentHealth > 0f;

    private void OnEnable()
    {
        if (!ActiveEnemyList.Contains(this))
        {
            ActiveEnemyList.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveEnemyList.Remove(this);
    }

    public void Initialize(
        TowerDefenseGameManager manager,
        TowerDefenseWaveSpawner spawner,
        IReadOnlyList<Transform> waypoints,
        EnemyType type)
    {
        gameManager = manager;
        waveSpawner = spawner;
        path = waypoints;
        enemyType = type;
        waypointIndex = 0;

        EnemyStats stats = TowerDefenseBalance.GetEnemyStats(type);
        currentHealth = stats.MaxHealth;
        currentMoveSpeed = stats.MoveSpeed;
        slowMultiplier = 1f;
        slowUntilTime = 0f;

        transform.localScale = stats.Scale;

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = stats.Color;
        }
    }

    private void Update()
    {
        if (gameManager == null || path == null || path.Count == 0 || !IsAlive)
        {
            return;
        }

        if (slowUntilTime > 0f && Time.time > slowUntilTime)
        {
            slowMultiplier = 1f;
            slowUntilTime = 0f;
        }

        if (waypointIndex >= path.Count)
        {
            ReachBase();
            return;
        }

        Transform targetWaypoint = path[waypointIndex];
        if (targetWaypoint == null)
        {
            waypointIndex++;
            return;
        }

        Vector3 targetPosition = targetWaypoint.position + Vector3.up * 0.55f;
        float step = currentMoveSpeed * slowMultiplier * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

        Vector3 lookDirection = targetPosition - transform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lookDirection.normalized),
                Time.deltaTime * 10f);
        }

        if (Vector3.Distance(transform.position, targetPosition) <= 0.05f)
        {
            waypointIndex++;
        }
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive)
        {
            return;
        }

        currentHealth -= damage;
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void ApplySlow(float multiplier, float duration)
    {
        slowMultiplier = Mathf.Clamp(multiplier, 0.2f, 1f);
        slowUntilTime = Mathf.Max(slowUntilTime, Time.time + duration);
    }

    private void Die()
    {
        EnemyStats stats = TowerDefenseBalance.GetEnemyStats(enemyType);
        gameManager.OnEnemyDefeated(stats.Reward);
        waveSpawner.NotifyEnemyRemoved(this);
        Destroy(gameObject);
    }

    private void ReachBase()
    {
        EnemyStats stats = TowerDefenseBalance.GetEnemyStats(enemyType);
        gameManager.OnEnemyReachedBase(stats.BaseDamage);
        waveSpawner.NotifyEnemyRemoved(this);
        Destroy(gameObject);
    }
}
