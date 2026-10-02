using UnityEngine;

public class TowerDefenseTowerSlot : MonoBehaviour
{
    [SerializeField] private Transform buildAnchor;

    private TowerDefenseTower builtTower;
    private Renderer slotRenderer;
    private Color baseColor;

    public bool IsOccupied => builtTower != null;
    public TowerType? BuiltTowerType => builtTower != null ? builtTower.TowerType : null;
    public Vector3 BuildPosition => (buildAnchor != null ? buildAnchor.position : transform.position) + Vector3.up * 0.9f;

    private void Awake()
    {
        slotRenderer = GetComponent<Renderer>();
        if (slotRenderer != null)
        {
            baseColor = slotRenderer.material.color;
        }
    }

    private void OnMouseDown()
    {
        if (TowerDefenseGameManager.Instance == null)
        {
            return;
        }

        TowerDefenseGameManager.Instance.TryBuildTower(this);
    }

    private void OnMouseEnter()
    {
        if (slotRenderer != null && !IsOccupied)
        {
            slotRenderer.material.color = baseColor * 1.15f;
        }
    }

    private void OnMouseExit()
    {
        RefreshVisual();
    }

    public bool BuildTower(TowerType towerType)
    {
        if (IsOccupied)
        {
            return false;
        }

        GameObject towerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        towerObject.name = $"{towerType}Tower";
        towerObject.transform.position = BuildPosition;
        towerObject.transform.SetParent(transform, true);

        Collider towerCollider = towerObject.GetComponent<Collider>();
        if (towerCollider != null)
        {
            Destroy(towerCollider);
        }

        builtTower = towerObject.AddComponent<TowerDefenseTower>();
        builtTower.Configure(towerType);
        RefreshVisual();
        return true;
    }

    private void RefreshVisual()
    {
        if (slotRenderer == null)
        {
            return;
        }

        slotRenderer.material.color = IsOccupied ? new Color(0.19f, 0.29f, 0.34f) : baseColor;
    }
}
