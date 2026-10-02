using System;
using System.Collections.Generic;
using UnityEngine;

public enum TowerType
{
    Arrow = 0,
    Cannon = 1,
    Slow = 2
}

public enum EnemyType
{
    Normal = 0,
    Fast = 1,
    Heavy = 2
}

[Serializable]
public class EnemySpawnInstruction
{
    public EnemyType enemyType = EnemyType.Normal;
    public int count = 4;
    public float interval = 0.8f;
}

[Serializable]
public class WaveDefinition
{
    public string strategySummary = "敌军正在试探中路防线。";
    public int completionReward = 20;
    public List<EnemySpawnInstruction> spawns = new();
}

public readonly struct TowerStats
{
    public TowerStats(float range, float cooldown, float damage, int cost, float slowFactor = 1f, float slowDuration = 0f)
    {
        Range = range;
        Cooldown = cooldown;
        Damage = damage;
        Cost = cost;
        SlowFactor = slowFactor;
        SlowDuration = slowDuration;
    }

    public float Range { get; }
    public float Cooldown { get; }
    public float Damage { get; }
    public int Cost { get; }
    public float SlowFactor { get; }
    public float SlowDuration { get; }
}

public readonly struct EnemyStats
{
    public EnemyStats(float moveSpeed, float maxHealth, int reward, int baseDamage, Color color, Vector3 scale)
    {
        MoveSpeed = moveSpeed;
        MaxHealth = maxHealth;
        Reward = reward;
        BaseDamage = baseDamage;
        Color = color;
        Scale = scale;
    }

    public float MoveSpeed { get; }
    public float MaxHealth { get; }
    public int Reward { get; }
    public int BaseDamage { get; }
    public Color Color { get; }
    public Vector3 Scale { get; }
}

public static class TowerDefenseBalance
{
    public static TowerStats GetTowerStats(TowerType towerType)
    {
        return towerType switch
        {
            TowerType.Arrow => new TowerStats(range: 7f, cooldown: 0.7f, damage: 14f, cost: 35),
            TowerType.Cannon => new TowerStats(range: 6f, cooldown: 1.3f, damage: 26f, cost: 55),
            TowerType.Slow => new TowerStats(range: 5.5f, cooldown: 1.2f, damage: 8f, cost: 45, slowFactor: 0.55f, slowDuration: 1.5f),
            _ => new TowerStats(range: 6f, cooldown: 1f, damage: 10f, cost: 40)
        };
    }

    public static EnemyStats GetEnemyStats(EnemyType enemyType)
    {
        return enemyType switch
        {
            EnemyType.Fast => new EnemyStats(moveSpeed: 4.8f, maxHealth: 24f, reward: 10, baseDamage: 1, color: new Color(0.94f, 0.70f, 0.30f), scale: new Vector3(0.75f, 0.75f, 0.75f)),
            EnemyType.Heavy => new EnemyStats(moveSpeed: 2.3f, maxHealth: 70f, reward: 18, baseDamage: 2, color: new Color(0.74f, 0.36f, 0.34f), scale: new Vector3(1.15f, 1.15f, 1.15f)),
            _ => new EnemyStats(moveSpeed: 3.2f, maxHealth: 40f, reward: 12, baseDamage: 1, color: new Color(0.32f, 0.57f, 0.88f), scale: Vector3.one)
        };
    }

    public static List<WaveDefinition> CreateDefaultWaves()
    {
        return new List<WaveDefinition>
        {
            new WaveDefinition
            {
                strategySummary = "第一波为常规侦察，敌人沿中路缓慢推进。",
                completionReward = 25,
                spawns = new List<EnemySpawnInstruction>
                {
                    new EnemySpawnInstruction { enemyType = EnemyType.Normal, count = 5, interval = 0.9f }
                }
            },
            new WaveDefinition
            {
                strategySummary = "第二波提高机动性，尝试用快速单位压缩反应时间。",
                completionReward = 30,
                spawns = new List<EnemySpawnInstruction>
                {
                    new EnemySpawnInstruction { enemyType = EnemyType.Fast, count = 6, interval = 0.7f },
                    new EnemySpawnInstruction { enemyType = EnemyType.Normal, count = 3, interval = 0.8f }
                }
            },
            new WaveDefinition
            {
                strategySummary = "第三波派出重装单位测试火力密度。",
                completionReward = 35,
                spawns = new List<EnemySpawnInstruction>
                {
                    new EnemySpawnInstruction { enemyType = EnemyType.Heavy, count = 3, interval = 1.1f },
                    new EnemySpawnInstruction { enemyType = EnemyType.Normal, count = 5, interval = 0.8f }
                }
            },
            new WaveDefinition
            {
                strategySummary = "第四波改用快慢混编，从连续兵线中寻找突破口。",
                completionReward = 40,
                spawns = new List<EnemySpawnInstruction>
                {
                    new EnemySpawnInstruction { enemyType = EnemyType.Fast, count = 6, interval = 0.6f },
                    new EnemySpawnInstruction { enemyType = EnemyType.Heavy, count = 2, interval = 1.0f }
                }
            },
            new WaveDefinition
            {
                strategySummary = "最终波全线施压，敌方集中兵力冲击基地核心。",
                completionReward = 60,
                spawns = new List<EnemySpawnInstruction>
                {
                    new EnemySpawnInstruction { enemyType = EnemyType.Normal, count = 6, interval = 0.6f },
                    new EnemySpawnInstruction { enemyType = EnemyType.Fast, count = 5, interval = 0.55f },
                    new EnemySpawnInstruction { enemyType = EnemyType.Heavy, count = 3, interval = 0.95f }
                }
            }
        };
    }
}
