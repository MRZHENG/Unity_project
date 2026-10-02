using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class AITest : MonoBehaviour
{
    private const string DefaultApiUrl = "https://open.bigmodel.cn/api/paas/v4/chat/completions";
    private const long TooManyRequestsStatusCode = 429;

    [Header("API")]
    [SerializeField] private string apiKey = "";
    [SerializeField] private string apiUrl = DefaultApiUrl;
    [SerializeField] private string modelName = "glm-4-flash";
    [SerializeField] private string systemPrompt = "你是一名简洁、专业的 Unity AI 助手，请用中文回答。";

    [Header("Generation")]
    [Range(0f, 2f)]
    [SerializeField] private float temperature = 0.7f;

    [Range(32, 2048)]
    [SerializeField] private int maxTokens = 256;

    [SerializeField] private bool keepConversationHistory = true;
    [SerializeField] private int maxHistoryMessages = 10;

    [Header("UI")]
    [SerializeField] private InputField inputField;
    [SerializeField] private Text responseText;
    [SerializeField] private Button sendButton;

    private readonly List<ChatMessage> conversationHistory = new();
    private bool isSending;

    private void Start()
    {
        LoadSharedApiKeyIfNeeded();

        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendButtonClicked);
        }

        ResetConversation();
    }

    private void OnDestroy()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(OnSendButtonClicked);
        }
    }

    private void OnValidate()
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            ZhipuApiKeyStore.Save(apiKey);
        }
    }

    [ContextMenu("Clear Conversation")]
    public void ResetConversation()
    {
        conversationHistory.Clear();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            conversationHistory.Add(new ChatMessage("system", systemPrompt.Trim()));
        }

        if (responseText != null)
        {
            responseText.text = "等待提问...";
        }
    }

    public void OnSendButtonClicked()
    {
        if (isSending)
        {
            return;
        }

        if (inputField == null || responseText == null || sendButton == null)
        {
            Debug.LogError("AITest 缺少 UI 组件引用，请在 Inspector 中完成绑定。");
            return;
        }

        string userInput = inputField.text.Trim();
        if (string.IsNullOrEmpty(userInput))
        {
            responseText.text = "请输入问题后再发送。";
            return;
        }

        LoadSharedApiKeyIfNeeded();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            responseText.text = "请先在 Inspector 中填写智谱 AI 的 API Key。";
            return;
        }

        ZhipuApiKeyStore.Save(apiKey);

        StartCoroutine(SendChatRequest(userInput));
    }

    private IEnumerator SendChatRequest(string userInput)
    {
        isSending = true;
        sendButton.interactable = false;

        if (!keepConversationHistory)
        {
            ClearConversationHistory();
        }

        AppendLine("我", userInput);
        inputField.text = string.Empty;
        inputField.ActivateInputField();

        conversationHistory.Add(new ChatMessage("user", userInput));
        TrimConversationHistory();

        string requestJson = JsonConvert.SerializeObject(new
        {
            model = modelName,
            messages = conversationHistory,
            temperature,
            max_tokens = maxTokens
        });

        using UnityWebRequest request = new(apiUrl, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {apiKey.Trim()}");

        yield return request.SendWebRequest();

        string responseJson = request.downloadHandler.text;
        Debug.Log("API 响应: " + responseJson);

        if (request.result != UnityWebRequest.Result.Success)
        {
            AppendLine("系统", BuildRequestErrorMessage(request, responseJson));
            isSending = false;
            sendButton.interactable = true;
            yield break;
        }

        ChatCompletionResponse response;
        try
        {
            response = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseJson);
        }
        catch (Exception exception)
        {
            AppendLine("系统", "响应解析失败，请检查 Console 中的原始 JSON。");
            Debug.LogException(exception);
            isSending = false;
            sendButton.interactable = true;
            yield break;
        }

        string assistantReply = response?.choices != null && response.choices.Count > 0
            ? response.choices[0]?.message?.content
            : null;

        if (string.IsNullOrWhiteSpace(assistantReply))
        {
            AppendLine("系统", "接口返回成功，但未解析到有效回复。");
        }
        else
        {
            assistantReply = assistantReply.Trim();
            conversationHistory.Add(new ChatMessage("assistant", assistantReply));
            TrimConversationHistory();
            AppendLine("AI", assistantReply);
        }

        isSending = false;
        sendButton.interactable = true;
    }

    private void AppendLine(string speaker, string message)
    {
        if (responseText == null)
        {
            return;
        }

        string prefix = string.IsNullOrWhiteSpace(responseText.text) || responseText.text == "等待提问..."
            ? string.Empty
            : responseText.text + "\n\n";

        responseText.text = $"{prefix}{speaker}：{message}";
    }

    private void ClearConversationHistory()
    {
        conversationHistory.Clear();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            conversationHistory.Add(new ChatMessage("system", systemPrompt.Trim()));
        }
    }

    private void TrimConversationHistory()
    {
        int systemMessageCount = string.IsNullOrWhiteSpace(systemPrompt) ? 0 : 1;
        int maxMessageCount = Mathf.Max(systemMessageCount + 2, maxHistoryMessages + systemMessageCount);

        while (conversationHistory.Count > maxMessageCount)
        {
            conversationHistory.RemoveAt(systemMessageCount);
        }
    }

    private string BuildRequestErrorMessage(UnityWebRequest request, string responseJson)
    {
        if (request.responseCode == TooManyRequestsStatusCode)
        {
            ApiErrorResponse errorResponse = TryDeserialize<ApiErrorResponse>(responseJson);
            string errorDetail = errorResponse?.error?.message;

            if (string.IsNullOrWhiteSpace(errorDetail))
            {
                errorDetail = "请求过于频繁，或当前 API Key 的调用额度已达上限。";
            }

            return $"请求失败：接口限流（429）。{errorDetail}";
        }

        ApiErrorResponse commonError = TryDeserialize<ApiErrorResponse>(responseJson);
        string commonDetail = commonError?.error?.message;

        if (!string.IsNullOrWhiteSpace(commonDetail))
        {
            return $"请求失败：HTTP/{request.responseCode} {commonDetail}";
        }

        return $"请求失败：HTTP/{request.responseCode} {request.error}";
    }

    private static T TryDeserialize<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch
        {
            return null;
        }
    }

    private void LoadSharedApiKeyIfNeeded()
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            ZhipuApiKeyStore.Save(apiKey);
            return;
        }

        string sharedApiKey = ZhipuApiKeyStore.Load();
        if (!string.IsNullOrWhiteSpace(sharedApiKey))
        {
            apiKey = sharedApiKey;
        }
    }

    [Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;

        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }

    [Serializable]
    private class ChatCompletionResponse
    {
        [JsonProperty("choices")]
        public List<ChatChoice> choices;
    }

    [Serializable]
    private class ChatChoice
    {
        [JsonProperty("message")]
        public ChatResponseMessage message;
    }

    [Serializable]
    private class ChatResponseMessage
    {
        [JsonProperty("content")]
        public string content;
    }

    [Serializable]
    private class ApiErrorResponse
    {
        [JsonProperty("error")]
        public ApiError error;
    }

    [Serializable]
    private class ApiError
    {
        [JsonProperty("message")]
        public string message;
    }
}
