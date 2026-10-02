using UnityEngine;

public class TowerDefenseProjectile : MonoBehaviour
{
    private TowerDefenseEnemy target;
    private float damage;
    private float slowFactor = 1f;
    private float slowDuration;
    private float moveSpeed;
    private Color impactColor;
    private Vector3 impactScale;

    public void Initialize(TowerDefenseEnemy targetEnemy, float projectileDamage, float projectileSpeed, float projectileSlowFactor, float projectileSlowDuration, Color projectileColor, Vector3 projectileScale)
    {
        target = targetEnemy;
        damage = projectileDamage;
        moveSpeed = projectileSpeed;
        slowFactor = projectileSlowFactor;
        slowDuration = projectileSlowDuration;
        transform.localScale = projectileScale;
        impactColor = projectileColor;
        impactScale = projectileScale * 3.4f;

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = projectileColor;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer.material.HasProperty("_EmissionColor"))
            {
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", projectileColor * 1.6f);
            }
        }

        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.14f;
        trail.startWidth = projectileScale.x * 0.95f;
        trail.endWidth = projectileScale.x * 0.18f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0.95f);
        trail.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0.08f);

        Light projectileLight = gameObject.AddComponent<Light>();
        projectileLight.type = LightType.Point;
        projectileLight.range = Mathf.Max(1.5f, projectileScale.x * 8f);
        projectileLight.intensity = 1.6f;
        projectileLight.color = projectileColor;
    }

    private void Update()
    {
        if (target == null || !target.IsAlive)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition = target.Position + Vector3.up * 0.45f;
        Vector3 direction = targetPosition - transform.position;
        float distanceThisFrame = moveSpeed * Time.deltaTime;

        if (direction.sqrMagnitude <= distanceThisFrame * distanceThisFrame)
        {
            HitTarget();
            return;
        }

        transform.position += direction.normalized * distanceThisFrame;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    private void HitTarget()
    {
        CreateImpactFlash();

        if (target != null && target.IsAlive)
        {
            target.TakeDamage(damage);
            if (slowFactor < 0.99f && slowDuration > 0f)
            {
                target.ApplySlow(slowFactor, slowDuration);
            }
        }

        Destroy(gameObject);
    }

    private void CreateImpactFlash()
    {
        GameObject flashObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flashObject.name = "ImpactFlash";
        flashObject.transform.position = transform.position;

        Collider flashCollider = flashObject.GetComponent<Collider>();
        if (flashCollider != null)
        {
            Destroy(flashCollider);
        }

        TowerDefenseImpactFlash flash = flashObject.AddComponent<TowerDefenseImpactFlash>();
        flash.Initialize(impactColor, impactScale);
    }
}
