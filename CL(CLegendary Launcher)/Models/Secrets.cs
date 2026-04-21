using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Class
{
    public static class Secrets
    {
        public const string DNS_KEY_AutoReport = "ВАШ_КЛЮЧ";
        public const string API_KEY_Gemini = "Ваш-API";
        public const string API_URL_Gemini_Model = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
        public static string LocalizationURL = $"https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/languages.json?v={DateTime.Now.Ticks}";
        public static string FundraiserURL => $"https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/fundraiser.json?v={DateTime.Now.Ticks}";
        public const string CreditsUrl = "Тут-Ваше-посилання";
        public const string CurseForgeKey = "Ваш-API";
        public const string EulaUrl = "YOUR_EULA_URL_HERE";
        public const string NewsUrl = "https://raw.githubusercontent.com/WER-CORE/CL-OpenSource/main/NewsListTest.json";
        public const string ManifestUrlOmni = "https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/omni_versions.json";
        public const string AuthUrlLittleSkin = "https://littleskin.cn/api/yggdrasil/authserver/authenticate";
        public const string _serversUrl = "https://raw.githubusercontent.com/WER-CORE/CL-OpenSource/main/serverList.json";
        public const string updateInfoUrl = "https://raw.githubusercontent.com/WER-CORE/CL-OpenSource/main/update.json";
        public const string UpdateUrlCheckLoadScreen = "https://raw.githubusercontent.com/WER-CORE/CL-OpenSource/main/update.json";
    }
}
