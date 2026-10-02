using UnityEngine;

public class TowerDefenseTower : MonoBehaviour
{
    [SerializeField] private TowerType towerType = TowerType.Arrow;

    private float attackRange;
    private float attackCooldown;
    private float attackDamage;
    private float slowFactor = 1f;
    private float slowDuration;
    private float nextAttackTime;
    private float projectileSpeed;
    private Color projectileColor;
    private Vector3 projectileScale;

    public TowerType TowerType => towerType;

    public void Configure(TowerType type)
    {
        towerType = type;

        TowerStats stats = TowerDefenseBalance.GetTowerStats(type);
        attackRange = stats.Range;
        attackCooldown = stats.Cooldown;
        attackDamage = stats.Damage;
        slowFactor = stats.SlowFactor;
        slowDuration = stats.SlowDuration;
        projectileSpeed = type switch
        {
            TowerType.Cannon => 10f,
            TowerType.Slow => 8.5f,
            _ => 13f
        };
        projectileColor = type switch
        {
            TowerType.Cannon => new Color(0.98f, 0.52f, 0.24f),
            TowerType.Slow => new Color(0.28f, 0.96f, 0.78f),
            _ => new Color(1.00f, 0.92f, 0.22f)
        };
        projectileScale = type switch
        {
            TowerType.Cannon => new Vector3(0.62f, 0.62f, 0.62f),
            TowerType.Slow => new Vector3(0.46f, 0.46f, 0.46f),
            _ => new Vector3(0.40f, 0.40f, 0.40f)
        };

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = type switch
            {
                TowerType.Cannon => new Color(0.76f, 0.42f, 0.34f),
                TowerType.Slow => new Color(0.40f, 0.70f, 0.64f),
                _ => new Color(0.36f, 0.56f, 0.88f)
            };
        }

        transform.localScale = type switch
        {
            TowerType.Cannon => new Vector3(1.1f, 2.1f, 1.1f),
            TowerType.Slow => new Vector3(0.95f, 1.7f, 0.95f),
            _ => new Vector3(0.85f, 1.8f, 0.85f)
        };
    }

    private void Awake()
    {
        Configure(towerType);
    }

    private void Update()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        TowerDefenseEnemy target = AcquireTarget();
        if (target == null)
        {
            return;
        }

        RotateTowards(target);
        if (!SpawnProjectile(target))
        {
            target.TakeDamage(attackDamage);
            if (slowFactor < 0.99f && slowDuration > 0f)
            {
                target.ApplySlow(slowFactor, slowDuration);
            }
        }

        nextAttackTime = Time.time + attackCooldown;
    }

    private TowerDefenseEnemy AcquireTarget()
    {
        TowerDefenseEnemy bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (TowerDefenseEnemy enemy in TowerDefenseEnemy.ActiveEnemies)
        {
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, enemy.Position);
            if (distance > attackRange || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = enemy;
        }

        return bestTarget;
    }

    private void RotateTowards(TowerDefenseEnemy target)
    {
        if (target == null)
        {
            return;
        }

        Vector3 lookDirection = target.Position - transform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lookDirection.normalized),
                Time.deltaTime * 16f);
        }
    }

    private bool SpawnProjectile(TowerDefenseEnemy target)
    {
        if (target == null)
        {
            return false;
        }

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = $"{towerType}Projectile";
        projectileObject.transform.position = transform.position + Vector3.up * 1.05f;

        Collider projectileCollider = projectileObject.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            Destroy(projectileCollider);
        }

        TowerDefenseProjectile projectile = projectileObject.AddComponent<TowerDefenseProjectile>();
        projectile.Initialize(target, attackDamage, projectileSpeed, slowFactor, slowDuration, projectileColor, projectileScale);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.25f, 0.80f, 0.35f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, attackRange > 0f ? attackRange : 6f);
    }
}
