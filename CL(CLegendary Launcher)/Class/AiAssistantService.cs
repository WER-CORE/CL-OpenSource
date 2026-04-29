using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));

                string json = await _client.GetStringAsync("https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/knowledge_base.json", cts.Token);
                var data = JsonConvert.DeserializeObject<KnowledgeBaseRoot>(json);

                if (data?.KnownIssues != null)
                {
                    _knowledgeBase = data.KnownIssues;
                    await File.WriteAllTextAsync(_localKbPath, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ШІ] Помилка онлайн-бази (перехід на локальну): {ex.Message}");
                await LoadLocalKnowledgeBaseAsync();
            }
        }

        private async Task LoadLocalKnowledgeBaseAsync()
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
                foreach (var issue in _knowledgeBase)
                {
                    if (issue.Keywords != null && issue.Keywords.Any() &&
                        issue.Keywords.All(k => logContent.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    {
                        return string.Format(LocalizationManager.GetString("AI.KnownIssueDetected", "[ВИЯВЛЕНО ВІДОМУ ПРОБЛЕМУ]\n{0}"), issue.Response);
                    }
                }
            }

            int maxLogLength = 12000;
            string truncatedLog = logContent.Length > maxLogLength
                ? logContent.Substring(logContent.Length - maxLogLength)
                : logContent;

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
                    answer = Regex.Replace(answer, @"(\*\*|\*|`|##)", "");
                }

                return string.IsNullOrWhiteSpace(answer) ?
                    LocalizationManager.GetString("AI.EmptyResponse", "Bit-CL не зміг розшифрувати дані. Відповідь порожня.") : answer.Trim();
            }
            catch (Exception ex)
            {
                return string.Format(LocalizationManager.GetString("AI.CriticalError", "Критична помилка комунікації з нейроядром: {0}"), ex.Message);
            }
        }
        private async Task<string> AskGeminiAsync(string prompt, string apiKey)
        {
            if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != Secrets.API_KEY_Gemini)
            {
                var googleRequestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } }
                };

                string googleJsonContent = JsonConvert.SerializeObject(googleRequestBody);
                using var googleHttpContent = new StringContent(googleJsonContent, Encoding.UTF8, "application/json");

                var googleResponse = await _client.PostAsync($"{Secrets.API_URL_Gemini_Model}?key={apiKey}", googleHttpContent);
                string googleResponseString = await googleResponse.Content.ReadAsStringAsync();

                if (!googleResponse.IsSuccessStatusCode)
                {
                    if (googleResponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        return LocalizationManager.GetString("AI.GeminiOverloaded", "Ваш особистий ключ Google вичерпав ліміт.");

                    return string.Format(LocalizationManager.GetString("AI.GeminiError", "Помилка сервера Gemini: {0}"), googleResponse.StatusCode);
                }

                var geminiResponse = JsonConvert.DeserializeObject<GeminiResponseModel>(googleResponseString);
                return geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            }

            var vercelRequestBody = new { prompt = prompt };
            string vercelJsonContent = JsonConvert.SerializeObject(vercelRequestBody);
            using var vercelHttpContent = new StringContent(vercelJsonContent, Encoding.UTF8, "application/json");

            var vercelResponse = await _client.PostAsync(Secrets.API_KEY_Gemini, vercelHttpContent);
            string vercelResponseString = await vercelResponse.Content.ReadAsStringAsync();

            if (!vercelResponse.IsSuccessStatusCode)
            {
                if (vercelResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorData = JsonConvert.DeserializeObject<VercelResponseModel>(vercelResponseString);
                    return errorData?.Answer ?? "Помилка запиту до проксі.";
                }

                return string.Format(LocalizationManager.GetString("AI.ServerError", "Помилка проксі-сервера CL: {0}"), vercelResponse.StatusCode);
            }

            var vercelData = JsonConvert.DeserializeObject<VercelResponseModel>(vercelResponseString);
            return vercelData?.Answer;
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
            using var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _client.PostAsync(OLLAMA_URL, httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return string.Format(LocalizationManager.GetString("AI.OllamaModelNotFound", "Модель '{0}' не знайдено..."), modelName);

                    return string.Format(LocalizationManager.GetString("AI.OllamaError", "Помилка сервера Ollama: {0}"), response.StatusCode);
                }

                string responseString = await response.Content.ReadAsStringAsync();

                var ollamaResponse = JsonConvert.DeserializeObject<OllamaResponseModel>(responseString);
                return ollamaResponse?.Response;
            }
            catch (HttpRequestException)
            {
                return LocalizationManager.GetString("AI.OllamaConnectionFailed", "Не вдалося підключитися до Ollama...");
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
    public class GeminiResponseModel
    {
        [JsonProperty("candidates")] public List<GeminiCandidate> Candidates { get; set; }
    }
    public class GeminiCandidate
    {
        [JsonProperty("content")] public GeminiContent Content { get; set; }
    }
    public class GeminiContent
    {
        [JsonProperty("parts")] public List<GeminiPart> Parts { get; set; }
    }
    public class GeminiPart
    {
        [JsonProperty("text")] public string Text { get; set; }
    }
    public class OllamaResponseModel
    {
        [JsonProperty("response")] public string Response { get; set; }
    }
    public class VercelResponseModel
    {
        [JsonProperty("answer")]
        public string Answer { get; set; }
    }
}