using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public class PatchNotesResponse
    {
        [JsonPropertyName("entries")]
        public List<PatchNoteEntry> Entries { get; set; }
    }
    public class FullPatchNote
    {
        [JsonPropertyName("body")]
        public string Body { get; set; }
    }
    public class PatchNoteEntry
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("shortText")]
        public string ShortText { get; set; }

        [JsonPropertyName("contentPath")]
        public string ContentPath { get; set; }
    }
    public class ChangelogService
    {
        private const string BASE_URL = "https://launchercontent.mojang.com/v2/";
        private const string PATCH_NOTES_URL = BASE_URL + "javaPatchNotes.json";
        public async Task<string> GetFullChangelogMarkdownAsync(string targetVersion)
        {
            try
            {
                using HttpClient client = new HttpClient();

                string mainJson = await client.GetStringAsync(PATCH_NOTES_URL);
                var response = JsonSerializer.Deserialize<PatchNotesResponse>(mainJson);
                var entry = response?.Entries?.FirstOrDefault(e => e.Version == targetVersion);

                if (entry == null) return null;

                if (string.IsNullOrEmpty(entry.ContentPath)) return $"# {entry.Title}\n\n{entry.ShortText}";

                string fullArticleUrl = BASE_URL + entry.ContentPath;
                string articleJson = await client.GetStringAsync(fullArticleUrl);

                var fullNote = JsonSerializer.Deserialize<FullPatchNote>(articleJson);

                if (string.IsNullOrEmpty(fullNote?.Body)) return $"# {entry.Title}\n\n{entry.ShortText}";

                string markdown = HtmlToMarkdown(fullNote.Body);

                return $"# {entry.Title}\n\n{markdown}";
            }
            catch
            {
                return null;
            }
        }
        private string AutoFormatTranslatedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            text = text.Replace("•", "*");

            text = Regex.Replace(text, @"([a-zA-Zа-яА-ЯіїєґІЇЄҐ0-9.,!?:;>])\s+-\s+", "$1\n* ");
            text = Regex.Replace(text, @"([a-zA-Zа-яА-ЯіїєґІЇЄҐ0-9.,!?:;>])\s+\*\s+", "$1\n* ");

            text = Regex.Replace(text, @"([^\n])\n\*\s+", "$1\n\n* ");

            text = Regex.Replace(text, @"(#{1,3}\s+.*?)\n([^\n])", "$1\n\n$2");

            text = Regex.Replace(text, @"\n\s+\*", "\n*");

            return text.Trim();
        }
        private string HtmlToMarkdown(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;

            string md = html;

            md = System.Net.WebUtility.HtmlDecode(md);

            md = Regex.Replace(md, @"<h1[^>]*>", "\n# ", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"</h1>", "\n\n", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<h2[^>]*>", "\n## ", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"</h2>", "\n\n", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<h3[^>]*>", "\n### ", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"</h3>", "\n\n", RegexOptions.IgnoreCase);

            md = Regex.Replace(md, @"<strong[^>]*>", "**", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"</strong>", "**", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<b[^>]*>", "**", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"</b>", "**", RegexOptions.IgnoreCase);

            md = Regex.Replace(md, @"<em[^>]*>", "*", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"</em>", "*", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<i[^>]*>", "*", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"</i>", "*", RegexOptions.IgnoreCase);

            md = Regex.Replace(md, @"<li[^>]*>", "- ", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"</li>", "\n", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"<p[^>]*>", "", RegexOptions.IgnoreCase);
            md = Regex.Replace(md, @"</p>", "\n\n", RegexOptions.IgnoreCase);

            md = Regex.Replace(md, @"<a[^>]+href=""([^""]+)""[^>]*>(.*?)</a>", "[$2]($1)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            md = Regex.Replace(md, @"<[^>]+>", string.Empty);

            md = Regex.Replace(md, @"\n{3,}", "\n\n");

            return md.Trim();
        }
        public async Task<string> TranslateTextAsync(string textToTranslate, string langCode)
        {
            if (string.IsNullOrWhiteSpace(textToTranslate)) return textToTranslate;
            if (langCode.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return textToTranslate;

            string targetLang = langCode.Length >= 2 ? langCode.Substring(0, 2).ToLower() : "uk";

            string[] paragraphs = textToTranslate.Split(new[] { "\n\n" }, StringSplitOptions.None);

            var batches = new System.Collections.Generic.List<string>();
            var currentBatch = new System.Text.StringBuilder();

            foreach (var p in paragraphs)
            {
                if (currentBatch.Length + p.Length > 3000)
                {
                    batches.Add(currentBatch.ToString());
                    currentBatch.Clear();
                }
                currentBatch.Append(p).Append("\n\n");
            }
            if (currentBatch.Length > 0) batches.Add(currentBatch.ToString());

            var translatedChangelog = new System.Text.StringBuilder();
            foreach (var batch in batches)
            {
                string translated = await SendGoogleTranslateRequestAsync(batch.Trim(), targetLang);

                translated = translated.Replace("** ", "**").Replace(" **", "**");
                translated = translated.Replace("# ", "# ");

                translatedChangelog.Append(translated).Append("\n\n");

                await Task.Delay(300);
            }

            string finalText = AutoFormatTranslatedText(translatedChangelog.ToString());

            return finalText;
        }
        private async Task<string> SendGoogleTranslateRequestAsync(string text, string targetLang)
        {
            try
            {
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl={targetLang}&dt=t&q={Uri.EscapeDataString(text)}";
                using HttpClient client = new HttpClient();
                string response = await client.GetStringAsync(url);

                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                string result = "";

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    foreach (var item in root[0].EnumerateArray())
                    {
                        result += item[0].GetString();
                    }
                }
                return string.IsNullOrEmpty(result) ? text : result;
            }
            catch { return text; }
        }

    }
}