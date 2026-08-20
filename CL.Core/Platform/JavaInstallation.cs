using System;

namespace CL.Core.Services
{
    public sealed class JavaInstallation
    {
        public string Executable { get; init; } = string.Empty;
        public int MajorVersion { get; init; }
        public string? Vendor { get; init; }
        public string? Architecture { get; init; }
        public string Source { get; init; } = "system";

        // Стабільний між запусками - щоб зберігати вибір користувача
        public string Id => $"java-{MajorVersion}-{ShortHash(Executable)}";

        public override string ToString() => Executable;

        private static string ShortHash(string input)
        {
            byte[] digest = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(input));

            return Convert.ToHexString(digest, 0, 6).ToLowerInvariant();
        }
    }
}
