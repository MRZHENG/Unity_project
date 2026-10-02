using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class TowerDefenseStrategyPlanner : MonoBehaviour
{
    private const string DefaultApiUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
    private const string DebugServerUrl = "http://127.0.0.1:7777/event";
    private const string DebugSessionId = "ai-parse-flaky";
    private const string DebugRunId = "pre-fix";

    [Header("API")]
    [SerializeField] private string apiKey = "";
    [SerializeField] private string apiUrl = DefaultApiUrl;
    [SerializeField] private string modelName = "glm-4-flash";
    [SerializeField] private string systemPrompt =
        "你是一名塔防游戏的敌方战术指挥官。你必须只返回 JSON，不要输出解释文字。JSON 必须包含 strategy 和 wave_config 两个字段，敌人类型只能为 fast、normal、heavy。";

    [Header("Generation")]
    [Range(0f, 2f)]
    [SerializeField] private float temperature = 0.4f;

    [Range(64, 2048)]
    [SerializeField] private int maxTokens = 512;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(apiKey);
    public string LastPlanSource { get; private set; } = "本地默认";
    public string LastPlannedSummary { get; private set; } = "等待开始第一波。";

    public string LastRequestJson { get; private set; }
    public string LastResponseJson { get; private set; }
    public string LastError { get; private set; }

    public IEnumerator RequestAndQueuePlan(
        TowerDefenseWaveSpawner spawner,
        int waveNumber,
        int availableEnemies,
        int enemiesSentLastWave,
        int enemiesArrivedLastWave,
        string weakSpot,
        Action<WaveDefinition> onCompleted = null)
    {
        if (spawner == null)
        {
            yield break;
        }

        string payload = BuildRequestJson(waveNumber, availableEnemies, enemiesSentLastWave, enemiesArrivedLastWave, weakSpot);
        LastRequestJson = payload;
        LastError = null;
        string resolvedApiKey = string.IsNullOrWhiteSpace(apiKey) ? string.Empty : apiKey.Trim();
        StartCoroutine(SendDebugEvent("A", "request-start", new
        {
            waveNumber,
            hasApiKey = !string.IsNullOrWhiteSpace(resolvedApiKey),
            modelName,
            temperature,
            maxTokens,
            requestPreview = payload.Length > 220 ? payload.Substring(0, 220) : payload
        }));

        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            LastPlanSource = "本地默认";
            WaveDefinition fallbackWave = BuildFallbackWave(waveNumber);
            fallbackWave.strategySummary = $"本地默认策略：{fallbackWave.strategySummary}";
            LastPlannedSummary = fallbackWave.strategySummary;
            spawner.QueuePlannedWave(fallbackWave);
            onCompleted?.Invoke(fallbackWave);
            yield break;
        }

        string bodyJson = JsonConvert.SerializeObject(new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt.Trim() },
                new { role = "user", content = payload }
            },
            temperature,
            max_tokens = maxTokens
        });

        using UnityWebRequest request = new(apiUrl, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {resolvedApiKey}");

        yield return request.SendWebRequest();

        LastResponseJson = request.downloadHandler.text;
        StartCoroutine(SendDebugEvent("A", "request-finished", new
        {
            waveNumber,
            result = request.result.ToString(),
            responseCode = request.responseCode,
            error = request.error,
            responsePreview = string.IsNullOrWhiteSpace(LastResponseJson) ? string.Empty : (LastResponseJson.Length > 420 ? LastResponseJson.Substring(0, 420) : LastResponseJson)
        }));
        if (request.result != UnityWebRequest.Result.Success)
        {
            LastError = $"HTTP/{request.responseCode} {request.error}";
            LastPlanSource = "本地默认";
            WaveDefinition fallbackWave = BuildFallbackWave(waveNumber);
            fallbackWave.strategySummary = $"接口失败，使用本地默认策略。{fallbackWave.strategySummary}";
            LastPlannedSummary = fallbackWave.strategySummary;
            spawner.QueuePlannedWave(fallbackWave);
            onCompleted?.Invoke(fallbackWave);
            yield break;
        }

        WaveDefinition plannedWave = TryParseWave(LastResponseJson, waveNumber) ?? BuildFallbackWave(waveNumber);
        if (LastPlanSource != "大模型策略")
        {
            plannedWave.strategySummary = $"解析失败，使用本地默认策略。{plannedWave.strategySummary}";
        }
        LastPlannedSummary = plannedWave.strategySummary;
        spawner.QueuePlannedWave(plannedWave);
        onCompleted?.Invoke(plannedWave);
    }

    public string BuildRequestJson(int waveNumber, int availableEnemies, int enemiesSentLastWave, int enemiesArrivedLastWave, string weakSpot)
    {
        WavePlanRequestPayload payload = new()
        {
            wave_number = waveNumber,
            defense_layout = CollectDefenseLayout(),
            previous_wave = new PreviousWavePayload
            {
                enemies_sent = enemiesSentLastWave,
                enemies_arrived = enemiesArrivedLastWave,
                weak_spot = string.IsNullOrWhiteSpace(weakSpot) ? "path_2" : weakSpot
            },
            available_enemies = availableEnemies
        };

        return JsonConvert.SerializeObject(payload, Formatting.Indented);
    }

    public WaveDefinition BuildFallbackWave(int waveNumber)
    {
        List<WaveDefinition> defaults = TowerDefenseBalance.CreateDefaultWaves();
        int index = Mathf.Clamp(waveNumber - 1, 0, defaults.Count - 1);
        return defaults[index];
    }

    private List<DefenseTowerPayload> CollectDefenseLayout()
    {
        TowerDefenseTowerSlot[] slots = FindObjectsOfType<TowerDefenseTowerSlot>();
        List<DefenseTowerPayload> layout = new();

        foreach (TowerDefenseTowerSlot slot in slots.Where(slot => slot != null && slot.IsOccupied && slot.BuiltTowerType.HasValue))
        {
            layout.Add(new DefenseTowerPayload
            {
                type = $"{slot.BuiltTowerType.Value.ToString().ToLowerInvariant()}_tower",
                position = FormatVector(slot.transform.position),
                level = 1
            });
        }

        return layout;
    }

    private WaveDefinition TryParseWave(string rawResponseJson, int targetWaveNumber)
    {
        try
        {
            string content = ExtractAssistantContent(rawResponseJson);
            StartCoroutine(SendDebugEvent("B", "assistant-content", new
            {
                targetWaveNumber,
                hasContent = !string.IsNullOrWhiteSpace(content),
                contentPreview = string.IsNullOrWhiteSpace(content) ? string.Empty : (content.Length > 300 ? content.Substring(0, 300) : content),
                finishReason = TryReadFinishReason(rawResponseJson)
            }));

            if (string.IsNullOrWhiteSpace(content))
            {
                LastError = "模型未返回可解析的策略内容。";
                LastPlanSource = "本地默认";
                return null;
            }

            string jsonPayload = ExtractJsonObject(content);
            StartCoroutine(SendDebugEvent("B", "json-payload", new
            {
                targetWaveNumber,
                hasJsonPayload = !string.IsNullOrWhiteSpace(jsonPayload),
                jsonPreview = string.IsNullOrWhiteSpace(jsonPayload) ? string.Empty : (jsonPayload.Length > 300 ? jsonPayload.Substring(0, 300) : jsonPayload)
            }));
            if (string.IsNullOrWhiteSpace(jsonPayload))
            {
                LastError = "模型回复中未提取到有效 JSON 对象。";
                LastPlanSource = "本地默认";
                return null;
            }

            JObject root = JObject.Parse(jsonPayload);
            string strategy = ReadStrategySummary(root["strategy"] ?? root["summary"] ?? root["plan"] ?? root["tactic"] ?? root["战术"] ?? root["策略"]);
            JToken waveConfigToken = root["wave_config"] ?? root["waveConfig"] ?? root["config"] ?? root["wave"];
            StartCoroutine(SendDebugEvent("C", "wave-config", new
            {
                targetWaveNumber,
                hasWaveConfig = waveConfigToken != null,
                waveConfigType = waveConfigToken?.Type.ToString(),
                rootKeys = root.Properties().Select(property => property.Name).ToArray()
            }));
            if (waveConfigToken == null)
            {
                LastError = "策略 JSON 缺少 wave_config 对象。";
                LastPlanSource = "本地默认";
                return null;
            }

            List<EnemySpawnInstruction> spawns = new();
            AppendSpawnsFromWaveConfig(spawns, waveConfigToken, targetWaveNumber);
            StartCoroutine(SendDebugEvent("C", "spawn-groups", new
            {
                targetWaveNumber,
                spawnGroupCount = spawns.Count,
                spawnTypes = spawns.Select(item => item.enemyType.ToString()).ToArray(),
                spawnCounts = spawns.Select(item => item.count).ToArray()
            }));

            if (spawns.Count == 0)
            {
                LastError = "策略中没有可用的出兵配置。";
                LastPlanSource = "本地默认";
                return null;
            }

            LastPlanSource = "大模型策略";

            return new WaveDefinition
            {
                strategySummary = string.IsNullOrWhiteSpace(strategy) ? "模型已返回策略，但未提供摘要。" : strategy.Trim(),
                completionReward = 35,
                spawns = spawns
            };
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            LastPlanSource = "本地默认";
            StartCoroutine(SendDebugEvent("D", "parse-exception", new
            {
                targetWaveNumber,
                exceptionType = exception.GetType().FullName,
                exception.Message,
                rawPreview = string.IsNullOrWhiteSpace(rawResponseJson) ? string.Empty : (rawResponseJson.Length > 420 ? rawResponseJson.Substring(0, 420) : rawResponseJson)
            }));
            return null;
        }
    }

    private IEnumerator SendDebugEvent(string hypothesisId, string location, object data)
    {
        string json = JsonConvert.SerializeObject(new
        {
            sessionId = DebugSessionId,
            runId = DebugRunId,
            hypothesisId,
            location,
            data,
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        using UnityWebRequest request = new(DebugServerUrl, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();
    }

    private static string TryReadFinishReason(string rawResponseJson)
    {
        if (string.IsNullOrWhiteSpace(rawResponseJson))
        {
            return null;
        }

        try
        {
            JObject envelope = JObject.Parse(rawResponseJson);
            return envelope["choices"]?.First?["finish_reason"]?.Value<string>();
        }
        catch
        {
            return null;
        }
    }

    private static void AppendSpawnsFromWaveConfig(List<EnemySpawnInstruction> spawns, JToken waveConfigToken, int targetWaveNumber)
    {
        if (waveConfigToken == null || waveConfigToken.Type == JTokenType.Null)
        {
            return;
        }

        if (waveConfigToken.Type == JTokenType.Object)
        {
            AppendSpawn(spawns, waveConfigToken["path_1"] ?? waveConfigToken["path1"] ?? waveConfigToken["left_path"] ?? waveConfigToken["left"]);
            AppendSpawn(spawns, waveConfigToken["path_2"] ?? waveConfigToken["path2"] ?? waveConfigToken["center_path"] ?? waveConfigToken["middle"] ?? waveConfigToken["center"]);
            AppendSpawn(spawns, waveConfigToken["path_3"] ?? waveConfigToken["path3"] ?? waveConfigToken["right_path"] ?? waveConfigToken["right"]);

            if (spawns.Count == 0)
            {
                AppendSpawn(spawns, waveConfigToken["enemies"]);
                AppendSpawn(spawns, waveConfigToken);
            }

            return;
        }

        if (waveConfigToken.Type != JTokenType.Array)
        {
            return;
        }

        JToken selectedWave = waveConfigToken
            .Children()
            .FirstOrDefault(item => ReadInt(item, "wave_number", "waveNumber", "wave") == targetWaveNumber) ?? waveConfigToken.First;

        if (selectedWave == null)
        {
            return;
        }

        AppendSpawn(spawns, selectedWave["enemies"]);
        AppendSpawn(spawns, selectedWave["path_1"] ?? selectedWave["path1"] ?? selectedWave["left_path"] ?? selectedWave["left"]);
        AppendSpawn(spawns, selectedWave["path_2"] ?? selectedWave["path2"] ?? selectedWave["center_path"] ?? selectedWave["middle"] ?? selectedWave["center"]);
        AppendSpawn(spawns, selectedWave["path_3"] ?? selectedWave["path3"] ?? selectedWave["right_path"] ?? selectedWave["right"]);

        if (spawns.Count == 0)
        {
            AppendSpawn(spawns, selectedWave);
        }
    }

    private static void AppendSpawn(List<EnemySpawnInstruction> spawns, JToken payload)
    {
        if (payload == null || payload.Type == JTokenType.Null)
        {
            return;
        }

        if (payload.Type == JTokenType.Array)
        {
            foreach (JToken item in payload.Children())
            {
                AppendSpawn(spawns, item);
            }

            return;
        }

        if (payload.Type != JTokenType.Object)
        {
            return;
        }

        if (payload["enemy_types"] != null && payload["enemy_count"] != null)
        {
            AppendSpawnFromEnemyTypes(spawns, payload);
            return;
        }

        int count = ReadInt(payload, "count", "num", "amount", "数量", "enemy_count", "enemyCount");
        if (count <= 0)
        {
            return;
        }

        string type = ReadString(payload, "type", "enemy_type", "enemyType", "kind", "兵种");
        float interval = ReadFloat(payload, 0.7f, "interval", "spawn_interval", "spawnInterval");

        spawns.Add(new EnemySpawnInstruction
        {
            enemyType = ParseEnemyType(type),
            count = Mathf.Clamp(count, 1, 20),
            interval = Mathf.Clamp(interval, 0.3f, 1.5f)
        });
    }

    private static void AppendSpawnFromEnemyTypes(List<EnemySpawnInstruction> spawns, JToken payload)
    {
        JToken enemyTypesToken = payload["enemy_types"] ?? payload["enemyTypes"];
        if (enemyTypesToken == null || enemyTypesToken.Type != JTokenType.Array)
        {
            return;
        }

        List<string> types = enemyTypesToken.Values<string>().Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        int totalCount = Mathf.Clamp(ReadInt(payload, "enemy_count", "enemyCount", "count", "num", "amount"), 1, 20);
        if (types.Count == 0 || totalCount <= 0)
        {
            return;
        }

        int baseCount = totalCount / types.Count;
        int remainder = totalCount % types.Count;
        float interval = ReadFloat(payload, 0.7f, "interval", "spawn_interval", "spawnInterval");

        for (int index = 0; index < types.Count; index++)
        {
            int count = baseCount + (index < remainder ? 1 : 0);
            if (count <= 0)
            {
                continue;
            }

            spawns.Add(new EnemySpawnInstruction
            {
                enemyType = ParseEnemyType(types[index]),
                count = count,
                interval = Mathf.Clamp(interval, 0.3f, 1.5f)
            });
        }
    }

    private static EnemyType ParseEnemyType(string type)
    {
        return type?.Trim().ToLowerInvariant() switch
        {
            "fast" => EnemyType.Fast,
            "heavy" => EnemyType.Heavy,
            _ => EnemyType.Normal
        };
    }

    private static string FormatVector(Vector3 position)
    {
        return $"({position.x:F1},{position.y:F1},{position.z:F1})";
    }

    private static string SanitizeJsonBlock(string content)
    {
        string trimmed = content.Trim();
        if (!trimmed.StartsWith("```"))
        {
            return trimmed;
        }

        string[] lines = trimmed.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= 2)
        {
            return trimmed.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();
        }

        return string.Join("\n", lines, 1, lines.Length - 2).Trim();
    }

    private static string ExtractAssistantContent(string rawResponseJson)
    {
        if (string.IsNullOrWhiteSpace(rawResponseJson))
        {
            return null;
        }

        JObject envelope = JObject.Parse(rawResponseJson);
        JToken choicesToken = envelope["choices"];
        JToken firstChoice = choicesToken != null && choicesToken.Type == JTokenType.Array ? choicesToken.First : null;
        JToken contentToken = firstChoice?["message"]?["content"];
        if (contentToken == null || contentToken.Type == JTokenType.Null)
        {
            return null;
        }

        if (contentToken.Type == JTokenType.String)
        {
            return contentToken.Value<string>();
        }

        if (contentToken.Type == JTokenType.Array)
        {
            StringBuilder builder = new();
            foreach (JToken part in contentToken.Children())
            {
                string text = part?["text"]?.Value<string>() ?? part?.ToString(Formatting.None);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    builder.AppendLine(text);
                }
            }

            return builder.ToString().Trim();
        }

        return contentToken.ToString(Formatting.None);
    }

    private static string ExtractJsonObject(string content)
    {
        string trimmed = SanitizeJsonBlock(content).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (LooksLikeJson(trimmed))
        {
            return trimmed;
        }

        int start = trimmed.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        bool inString = false;
        bool escaping = false;
        int depth = 0;
        for (int index = start; index < trimmed.Length; index++)
        {
            char current = trimmed[index];

            if (escaping)
            {
                escaping = false;
                continue;
            }

            if (current == '\\')
            {
                escaping = true;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current == '{')
            {
                depth++;
            }
            else if (current == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return trimmed.Substring(start, index - start + 1);
                }
            }
        }

        return null;
    }

    private static bool LooksLikeJson(string text)
    {
        return (text.StartsWith("{") && text.EndsWith("}")) || (text.StartsWith("[") && text.EndsWith("]"));
    }

    private static string ReadString(JToken token, params string[] keys)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        foreach (string key in keys)
        {
            JToken valueToken = token[key];
            if (valueToken == null || valueToken.Type == JTokenType.Null)
            {
                continue;
            }

            string value = valueToken.Type == JTokenType.String
                ? valueToken.Value<string>()
                : valueToken.ToString(Formatting.None);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string ReadStrategySummary(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return null;
        }

        if (token.Type == JTokenType.String)
        {
            return token.Value<string>()?.Trim();
        }

        if (token.Type == JTokenType.Object)
        {
            List<string> parts = new();
            foreach (JProperty property in token.Children<JProperty>())
            {
                string value = property.Value?.Type == JTokenType.String
                    ? property.Value.Value<string>()
                    : property.Value?.ToString(Formatting.None);

                if (!string.IsNullOrWhiteSpace(value))
                {
                    string label = TranslateStrategyKey(property.Name);
                    string friendlyValue = TranslateStrategyValue(value);
                    parts.Add(string.IsNullOrWhiteSpace(label) ? friendlyValue : $"{label}{friendlyValue}");
                }
            }

            return parts.Count > 0 ? string.Join("；", parts) : token.ToString(Formatting.None);
        }

        return TranslateStrategyValue(token.ToString(Formatting.None));
    }

    private static string TranslateStrategyKey(string key)
    {
        return key?.Trim().ToLowerInvariant() switch
        {
            "initial_wave" => "首波：",
            "mid_game" => "中期：",
            "end_game" => "后期：",
            "final_wave" => "终局：",
            "subsequent_waves" => "后续：",
            _ => string.IsNullOrWhiteSpace(key) ? string.Empty : $"{key}:"
        };
    }

    private static string TranslateStrategyValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "surprise_attack" => "快速突袭",
            "increase_density" => "提高兵线密度",
            "mixed_wave" => "快慢混编压制",
            "heavy_focus" => "重装单位集中推进",
            "fast" => "高速单位抢节奏",
            "normal" => "常规兵线推进",
            "heavy" => "重装单位推进",
            _ => value.Replace("_", " ").Trim()
        };
    }

    private static int ReadInt(JToken token, params string[] keys)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return 0;
        }

        foreach (string key in keys)
        {
            JToken valueToken = token[key];
            if (valueToken == null || valueToken.Type == JTokenType.Null)
            {
                continue;
            }

            if (valueToken.Type == JTokenType.Integer)
            {
                return valueToken.Value<int>();
            }

            if (valueToken.Type == JTokenType.Float)
            {
                return Mathf.RoundToInt(valueToken.Value<float>());
            }

            if (int.TryParse(valueToken.ToString(), out int parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static float ReadFloat(JToken token, float fallback, params string[] keys)
    {
        if (token == null || token.Type == JTokenType.Null)
        {
            return fallback;
        }

        foreach (string key in keys)
        {
            JToken valueToken = token[key];
            if (valueToken == null || valueToken.Type == JTokenType.Null)
            {
                continue;
            }

            if (valueToken.Type == JTokenType.Float || valueToken.Type == JTokenType.Integer)
            {
                return valueToken.Value<float>();
            }

            if (float.TryParse(valueToken.ToString(), out float parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    [Serializable]
    private class WavePlanRequestPayload
    {
        public int wave_number;
        public List<DefenseTowerPayload> defense_layout;
        public PreviousWavePayload previous_wave;
        public int available_enemies;
    }

    [Serializable]
    private class DefenseTowerPayload
    {
        public string type;
        public string position;
        public int level;
    }

    [Serializable]
    private class PreviousWavePayload
    {
        public int enemies_sent;
        public int enemies_arrived;
        public string weak_spot;
    }

}
