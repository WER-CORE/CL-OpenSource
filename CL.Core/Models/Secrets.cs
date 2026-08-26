using System;

namespace CL.Core.Services
{
    public static class Secrets
    {
        public const string DNS_KEY_AutoReport = "";
        public const string CLIENT_ID_AZURE = "";
        public const string API_KEY_Gemini = "";
        public const string API_URL_Gemini_Model = "";
        public const string CurseForgeKey = "";
        public static string LocalizationURL = $"https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/languages.json?v={DateTime.Now.Ticks}";
        public static string FundraiserURL => $"https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/fundraiser.json?v={DateTime.Now.Ticks}";
        public static string CreditsUrl => $"https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/credits.txt?v={DateTime.Now.Ticks}";
        public const string EulaUrl = "https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/";
        public const string NewsUrl = "https://drive.usercontent.google.com/u/0/uc?id=1di7dPobDy4s3Bbm7il90jObmPDS4Bwrf&export=download";
        public const string AuthUrlLittleSkin = "https://littleskin.cn/api/yggdrasil/authserver/authenticate";
        public const string _serversUrl = "https://drive.google.com/uc?export=download&id=1AsQhx-on-dRhQu4suNd5RZM8Bqgz-XyG";
        public static string UpdateUrlCheckLoadScreen = $"https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/update.json?v={DateTime.Now.Ticks}";
        public static string updateInfoUrl = $"https://raw.githubusercontent.com/WER-CORE/CL-Win-Edition--Update/main/update.json?v={DateTime.Now.Ticks}";
    }
}
