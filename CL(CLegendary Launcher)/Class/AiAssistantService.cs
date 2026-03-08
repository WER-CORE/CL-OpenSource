using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public enum AiProvider
    {
        Gemini,
        Ollama
    }

    public class AiAssistantService
    {
        private List<KnownIssue> _knowledgeBase;
        private static readonly HttpClient _client = new HttpClient();

        private readonly string _localKbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "knowledge_base.json");

        private const string OLLAMA_URL = "http://localhost:11434/api/generate";

        public async Task InitializeAsync()
        {
            _knowledgeBase = new List<KnownIssue>();

            try
            {
                string json = await _client.GetStringAsync("https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/knowledge_base.json");
                var data = JsonConvert.DeserializeObject<KnowledgeBaseRoot>(json);

                if (data?.KnownIssues != null)
                {
                    _knowledgeBase = data.KnownIssues;
                    await File.WriteAllTextAsync(_localKbPath, json);
                }
            }
            catch
            {
                if (File.Exists(_localKbPath))
                {
                    try
                    {
                        string localJson = await File.ReadAllTextAsync(_localKbPath);
                        var localData = JsonConvert.DeserializeObject<KnowledgeBaseRoot>(localJson);
                        if (localData?.KnownIssues != null)
                        {
                            _knowledgeBase = localData.KnownIssues;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ШІ] Помилка читання локальної бази: {ex.Message}");
                    }
                }
            }
        }
        public async Task AddToKnowledgeBaseAsync(string issueId, List<string> keywords, string responseText)
        {
            if (_knowledgeBase == null) _knowledgeBase = new List<KnownIssue>();

            var existingIssue = _knowledgeBase.FirstOrDefault(x => x.Id == issueId);
            if (existingIssue != null)
            {
                existingIssue.Keywords = keywords;
                existingIssue.Response = responseText;
            }
            else
            {
                _knowledgeBase.Add(new KnownIssue { Id = issueId, Keywords = keywords, Response = responseText });
            }

            var root = new KnowledgeBaseRoot { KnownIssues = _knowledgeBase };
            string jsonToSave = JsonConvert.SerializeObject(root, Formatting.Indented);
            await File.WriteAllTextAsync(_localKbPath, jsonToSave);
        }

        public async Task<string> AnalyzeCrashLogAsync(string logContent, AiProvider provider = AiProvider.Gemini, string customApiKey = null, string ollamaModel = "llama3")
        {
            if (string.IsNullOrWhiteSpace(logContent))
                return LocalizationManager.GetString("AI.EmptyLog", "Лог порожній. Немає даних для аналізу.");

            if (_knowledgeBase != null && _knowledgeBase.Count > 0)
            {
                string lowerLog = logContent.ToLower();

                foreach (var issue in _knowledgeBase)
                {
                    if (issue.Keywords != null && issue.Keywords.Any() && issue.Keywords.All(k => lowerLog.Contains(k.ToLower())))
                    {
                        return string.Format(LocalizationManager.GetString("AI.KnownIssueDetected", "[ВИЯВЛЕНО ВІДОМУ ПРОБЛЕМУ]\n{0}"), issue.Response);
                    }
                }
            }

            string truncatedLog = logContent.Length > 10000 ? logContent.Substring(logContent.Length - 10000) : logContent;

            string systemPrompt = LocalizationManager.GetString("AI.SystemPrompt", "Ти — Агент Сі-Ел (Agent C.L.)...");

            string fullPrompt = $"{systemPrompt}\n\n[ДАНІ ВІД BIT-CL (LOG)]:\n{truncatedLog}";
            string answer = "";

            try
            {
                switch (provider)
                {
                    case AiProvider.Gemini:
                        string activeApiKey = string.IsNullOrWhiteSpace(customApiKey) ? Secrets.API_KEY_Gemini : customApiKey;
                        answer = await AskGeminiAsync(fullPrompt, activeApiKey);
                        break;

                    case AiProvider.Ollama:
                        answer = await AskOllamaAsync(fullPrompt, ollamaModel);
                        break;
                }

                if (!string.IsNullOrWhiteSpace(answer))
                {
                    answer = answer.Replace("**", "").Replace("*", "").Replace("`", "").Replace("##", "");
                }

                return string.IsNullOrWhiteSpace(answer) ?
                    LocalizationManager.GetString("AI.EmptyResponse", "Bit-CL не зміг розшифрувати дані. Відповідь порожня.") : answer;
            }
            catch (Exception ex)
            {
                return string.Format(LocalizationManager.GetString("AI.CriticalError", "Критична помилка комунікації з нейроядром: {0}"), ex.Message);
            }
        }

        private async Task<string> AskGeminiAsync(string prompt, string apiKey)
        {
            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } }
            };

            string jsonContent = JsonConvert.SerializeObject(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync($"{Secrets.API_URL_Gemini_Model}?key={apiKey}", httpContent);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    return LocalizationManager.GetString("AI.GeminiOverloaded", "Сервери Google перевантажені або денний ліміт вичерпано. Використовуйте власний ключ або переключіться на локальну модель (Ollama).");

                return string.Format(LocalizationManager.GetString("AI.GeminiError", "Помилка сервера Gemini: {0}"), response.StatusCode);
            }

            dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);
            return jsonResponse?.candidates[0]?.content?.parts[0]?.text;
        }

        private async Task<string> AskOllamaAsync(string prompt, string modelName)
        {
            var requestBody = new
            {
                model = modelName,
                prompt = prompt,
                stream = false
            };

            string jsonContent = JsonConvert.SerializeObject(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _client.PostAsync(OLLAMA_URL, httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return string.Format(LocalizationManager.GetString("AI.OllamaModelNotFound", "Модель '{0}' не знайдено в Ollama. Завантажте її через консоль (наприклад: ollama run {0})."), modelName);

                    return string.Format(LocalizationManager.GetString("AI.OllamaError", "Помилка сервера Ollama: {0}"), response.StatusCode);
                }

                string responseString = await response.Content.ReadAsStringAsync();
                dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);

                return jsonResponse?.response;
            }
            catch (HttpRequestException)
            {
                return LocalizationManager.GetString("AI.OllamaConnectionFailed", "Не вдалося підключитися до Ollama. Переконайтеся, що програма Ollama запущена на вашому комп'ютері та працює у фоні.");
            }
        }
    }

    public class KnowledgeBaseRoot
    {
        [JsonProperty("known_issues")]
        public List<KnownIssue> KnownIssues { get; set; }
    }

    public class KnownIssue
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("keywords")] public List<string> Keywords { get; set; }
        [JsonProperty("response")] public string Response { get; set; }
    }
}