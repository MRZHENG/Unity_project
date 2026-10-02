using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TowerDefensePath : MonoBehaviour
{
    [SerializeField] private List<Transform> waypoints = new();

    public IReadOnlyList<Transform> Waypoints => waypoints;

    public void RebuildFromChildren()
    {
        waypoints = GetComponentsInChildren<Transform>()
            .Where(item => item != transform)
            .OrderBy(item => item.name)
            .ToList();
    }

    private void Awake()
    {
        if (waypoints.Count == 0)
        {
            RebuildFromChildren();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count < 2)
        {
            return;
        }

        Gizmos.color = new Color(0.18f, 0.82f, 0.88f);
        for (int index = 0; index < waypoints.Count - 1; index++)
        {
            if (waypoints[index] == null || waypoints[index + 1] == null)
            {
                continue;
            }

            Gizmos.DrawLine(waypoints[index].position, waypoints[index + 1].position);
        }
    }
}
