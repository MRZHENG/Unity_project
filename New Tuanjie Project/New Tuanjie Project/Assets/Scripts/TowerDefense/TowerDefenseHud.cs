using UnityEngine;
using UnityEngine.UI;

public class TowerDefenseHud : MonoBehaviour
{
    [SerializeField] private Text resourcesText;
    [SerializeField] private Text baseHealthText;
    [SerializeField] private Text waveText;
    [SerializeField] private Text selectionText;
    [SerializeField] private Text strategyText;
    [SerializeField] private Text statusText;
    [SerializeField] private Button nextWaveButton;

    private TowerDefenseGameManager gameManager;

    public void Bind(TowerDefenseGameManager manager)
    {
        gameManager = manager;

        if (nextWaveButton != null)
        {
            nextWaveButton.onClick.RemoveAllListeners();
            nextWaveButton.onClick.AddListener(OnNextWaveClicked);
        }
    }

    public void Refresh(
        int resources,
        int baseHealth,
        int currentWave,
        int totalWaves,
        TowerType selectedTowerType,
        string strategySummary,
        string statusSummary,
        bool canStartWave)
    {
        if (resourcesText != null)
        {
            resourcesText.text = $"资源：{resources}";
        }

        if (baseHealthText != null)
        {
            baseHealthText.text = $"基地生命：{baseHealth}";
        }

        if (waveText != null)
        {
            waveText.text = $"波次：{currentWave}/{totalWaves}";
        }

        if (selectionText != null)
        {
            int arrowCost = TowerDefenseBalance.GetTowerStats(TowerType.Arrow).Cost;
            int cannonCost = TowerDefenseBalance.GetTowerStats(TowerType.Cannon).Cost;
            int slowCost = TowerDefenseBalance.GetTowerStats(TowerType.Slow).Cost;
            int selectedCost = TowerDefenseBalance.GetTowerStats(selectedTowerType).Cost;
            selectionText.text =
                $"当前塔型：{selectedTowerType}（按 1/2/3 切换）\n" +
                $"当前建造费用：{selectedCost}\n" +
                $"Arrow:{arrowCost}  Cannon:{cannonCost}  Slow:{slowCost}";
        }

        if (strategyText != null)
        {
            strategyText.text = $"策略摘要：{strategySummary}";
        }

        if (statusText != null)
        {
            statusText.text = statusSummary;
        }

        if (nextWaveButton != null)
        {
            nextWaveButton.interactable = canStartWave;
        }
    }

    private void OnNextWaveClicked()
    {
        gameManager?.TryStartNextWave();
    }
}
