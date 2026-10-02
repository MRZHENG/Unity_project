using UnityEngine;

public static class ZhipuApiKeyStore
{
    private const string PlayerPrefsKey = "zhipu_api_key";

    public static string Load()
    {
        return PlayerPrefs.GetString(PlayerPrefsKey, string.Empty)?.Trim();
    }

    public static void Save(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        PlayerPrefs.SetString(PlayerPrefsKey, apiKey.Trim());
        PlayerPrefs.Save();
    }
}
