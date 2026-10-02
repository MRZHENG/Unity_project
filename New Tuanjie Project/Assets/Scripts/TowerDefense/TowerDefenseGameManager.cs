using System.Collections;
using UnityEngine;

public class TowerDefenseGameManager : MonoBehaviour
{
    public static TowerDefenseGameManager Instance { get; private set; }

    [Header("Progression")]
    [SerializeField] private int startingResources = 120;
    [SerializeField] private int baseHealth = 10;

    [Header("References")]
    [SerializeField] private TowerDefenseWaveSpawner waveSpawner;
    [SerializeField] private TowerDefenseHud hud;
    [SerializeField] private TowerDefenseStrategyPlanner strategyPlanner;

    private int currentResources;
    private int currentBaseHealth;
    private string strategySummary = "等待开始第一波。";
    private string statusSummary = "点击塔位建塔，按空格或按钮开始下一波。";
    private bool isGameFinished;
    private bool awaitingNextWave = true;
    private bool isPlanningWave;
    private int previousWaveEnemiesSent;
    private int previousWaveEnemiesArrived;
    private int currentWaveEnemiesSent;
    private int currentWaveEnemiesArrived;
    private string previousWeakSpot = "path_2";

    public int CurrentResources => currentResources;
    public int CurrentBaseHealth => currentBaseHealth;
    public TowerType SelectedTowerType { get; private set; } = TowerType.Arrow;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        currentResources = startingResources;
        currentBaseHealth = baseHealth;

        if (waveSpawner == null)
        {
            waveSpawner = FindObjectOfType<TowerDefenseWaveSpawner>();
        }

        if (hud == null)
        {
            hud = FindObjectOfType<TowerDefenseHud>();
        }

        if (strategyPlanner == null)
        {
            strategyPlanner = FindObjectOfType<TowerDefenseStrategyPlanner>();
        }

        waveSpawner?.Initialize(this);
        hud?.Bind(this);
        RefreshHud();
    }

    private void Update()
    {
        if (isGameFinished)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectTower(TowerType.Arrow);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectTower(TowerType.Cannon);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectTower(TowerType.Slow);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryStartNextWave();
        }
    }

    public void TryBuildTower(TowerDefenseTowerSlot slot)
    {
        if (isGameFinished || slot == null)
        {
            return;
        }

        if (slot.IsOccupied)
        {
            statusSummary = "该塔位已被占用。";
            RefreshHud();
            return;
        }

        int cost = TowerDefenseBalance.GetTowerStats(SelectedTowerType).Cost;
        if (currentResources < cost)
        {
            statusSummary = $"资源不足，建造 {SelectedTowerType} 需要 {cost}。";
            RefreshHud();
            return;
        }

        if (!slot.BuildTower(SelectedTowerType))
        {
            statusSummary = "建造失败，请重试。";
            RefreshHud();
            return;
        }

        currentResources -= cost;
        statusSummary = $"已建造 {SelectedTowerType} 塔，消耗资源 {cost}。";
        RefreshHud();
    }

    public void TryStartNextWave()
    {
        if (isGameFinished || waveSpawner == null)
        {
            return;
        }

        if (isPlanningWave)
        {
            statusSummary = "AI 正在规划下一波，请稍候。";
            RefreshHud();
            return;
        }

        if (!waveSpawner.CanStartNextWave())
        {
            statusSummary = waveSpawner.IsWaveRunning ? "当前波次尚未结束。" : "已完成全部波次。";
            RefreshHud();
            return;
        }

        StartCoroutine(PlanAndStartNextWaveCoroutine());
    }

    public void OnWaveStarted(int waveNumber, string summary, int enemiesSent)
    {
        currentWaveEnemiesSent = enemiesSent;
        currentWaveEnemiesArrived = 0;
        strategySummary = summary;
        statusSummary = $"第 {waveNumber} 波开始，请守住基地核心。";
        awaitingNextWave = false;
        isPlanningWave = false;
        RefreshHud();
    }

    public void OnWaveCompleted(int reward)
    {
        if (isGameFinished)
        {
            return;
        }

        currentResources += reward;
        awaitingNextWave = true;
        previousWaveEnemiesSent = currentWaveEnemiesSent;
        previousWaveEnemiesArrived = currentWaveEnemiesArrived;
        previousWeakSpot = EstimateWeakSpot(previousWaveEnemiesArrived, previousWaveEnemiesSent);

        if (waveSpawner != null && waveSpawner.CurrentWaveNumber >= waveSpawner.TotalWaveCount)
        {
            strategySummary = "所有波次已抵御完成。";
            EndGame(true, $"防守成功，最终获得 {currentResources} 资源。");
            return;
        }

        statusSummary = $"本波结束，获得资源奖励 {reward}。";
        RefreshHud();
    }

    public void OnEnemyDefeated(int reward)
    {
        if (isGameFinished)
        {
            return;
        }

        currentResources += reward;
        statusSummary = $"敌人被击败，获得资源 {reward}。";
        RefreshHud();
    }

    public void OnEnemyReachedBase(int damage)
    {
        if (isGameFinished)
        {
            return;
        }

        currentBaseHealth -= damage;
        currentWaveEnemiesArrived++;
        statusSummary = $"基地受到 {damage} 点伤害。";

        if (currentBaseHealth <= 0)
        {
            currentBaseHealth = 0;
            strategySummary = "防线被突破，基地核心已失守。";
            EndGame(false, "游戏失败，基地生命归零。");
            return;
        }

        RefreshHud();
    }

    private void SelectTower(TowerType towerType)
    {
        SelectedTowerType = towerType;
        int cost = TowerDefenseBalance.GetTowerStats(towerType).Cost;
        statusSummary = $"已切换到 {towerType} 塔，当前建造费用 {cost}。";
        RefreshHud();
    }

    private void EndGame(bool isVictory, string summary)
    {
        isGameFinished = true;
        awaitingNextWave = false;
        isPlanningWave = false;
        statusSummary = summary;
        if (isVictory)
        {
            strategySummary = "塔防演示原型已成功完成全部波次。";
        }

        RefreshHud();
    }

    private void RefreshHud()
    {
        hud?.Refresh(
            currentResources,
            currentBaseHealth,
            waveSpawner != null ? waveSpawner.CurrentWaveNumber : 0,
            waveSpawner != null ? waveSpawner.TotalWaveCount : 0,
            SelectedTowerType,
            strategySummary,
            statusSummary,
            !isGameFinished && !isPlanningWave && awaitingNextWave && waveSpawner != null && waveSpawner.CanStartNextWave());
    }

    private IEnumerator PlanAndStartNextWaveCoroutine()
    {
        isPlanningWave = true;
        awaitingNextWave = false;

        int nextWaveNumber = waveSpawner.CurrentWaveNumber + 1;
        statusSummary = strategyPlanner != null && strategyPlanner.HasApiKey
            ? $"AI 正在规划第 {nextWaveNumber} 波进攻策略..."
            : $"未填写 API Key，使用本地默认的第 {nextWaveNumber} 波配置。";
        RefreshHud();

        if (strategyPlanner != null)
        {
            int availableEnemies = EstimateEnemyBudget(nextWaveNumber);
            yield return StartCoroutine(strategyPlanner.RequestAndQueuePlan(
                waveSpawner,
                nextWaveNumber,
                availableEnemies,
                previousWaveEnemiesSent,
                previousWaveEnemiesArrived,
                previousWeakSpot));

            strategySummary = strategyPlanner.LastPlannedSummary;
            statusSummary = strategyPlanner.LastPlanSource == "大模型策略"
                ? $"已获取第 {nextWaveNumber} 波 AI 策略，准备开波。"
                : $"本轮使用本地默认波次。{(string.IsNullOrWhiteSpace(strategyPlanner.LastError) ? string.Empty : $"原因：{strategyPlanner.LastError}")}";
        }

        if (!waveSpawner.StartNextWave())
        {
            isPlanningWave = false;
            awaitingNextWave = true;
            statusSummary = "波次启动失败，请检查路径或生成器配置。";
            RefreshHud();
        }
    }

    private static string EstimateWeakSpot(int enemiesArrived, int enemiesSent)
    {
        if (enemiesSent <= 0)
        {
            return "path_2";
        }

        float arrivalRatio = enemiesArrived / (float)enemiesSent;
        if (arrivalRatio > 0.45f)
        {
            return "path_3";
        }

        if (arrivalRatio > 0.2f)
        {
            return "path_2";
        }

        return "path_1";
    }

    private static int EstimateEnemyBudget(int nextWaveNumber)
    {
        return Mathf.Clamp(8 + nextWaveNumber * 4, 10, 28);
    }
}
