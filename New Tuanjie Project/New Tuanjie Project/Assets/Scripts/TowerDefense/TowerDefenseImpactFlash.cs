using UnityEngine;

public class TowerDefenseImpactFlash : MonoBehaviour
{
    private float lifeTime = 0.18f;
    private float elapsedTime;
    private Vector3 startScale;
    private Vector3 targetScale;

    public void Initialize(Color flashColor, Vector3 scale)
    {
        startScale = scale * 0.35f;
        targetScale = scale;
        transform.localScale = startScale;

        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = flashColor;
        }
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(elapsedTime / lifeTime);
        transform.localScale = Vector3.Lerp(startScale, targetScale, t);

        if (elapsedTime >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}
