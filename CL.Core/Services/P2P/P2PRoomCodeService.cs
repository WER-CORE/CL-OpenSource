using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace CL.Core.Services.P2P;

public class P2PRoomPayload
{
    public string HostAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public string SessionSecret { get; set; } = string.Empty;
    public string WorldName { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string LoaderType { get; set; } = string.Empty;
    public string LoaderVersion { get; set; } = string.Empty;
    public string ModpackName { get; set; } = string.Empty;
    public string ModpackTypeSite { get; set; } = string.Empty;
}

public static class P2PRoomCodeService
{
    private static readonly byte[] EncryptionKey = new byte[]
    {
        0x43, 0x4C, 0x65, 0x67, 0x65, 0x6E, 0x64, 0x61,
        0x72, 0x79, 0x50, 0x32, 0x50, 0x4B, 0x65, 0x79,
        0x32, 0x30, 0x32, 0x36, 0x53, 0x65, 0x63, 0x75,
        0x72, 0x69, 0x74, 0x79, 0x4C, 0x6E, 0x63, 0x68
    };

    public static string GenerateRoomCode(P2PRoomPayload payload)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));

        string json = JsonConvert.SerializeObject(payload);
        byte[] plainBytes = Encoding.UTF8.GetBytes(json);

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                gzip.Write(plainBytes, 0, plainBytes.Length);
            }
            compressed = ms.ToArray();
        }

        byte[] iv = new byte[16];
        RandomNumberGenerator.Fill(iv);

        byte[] encrypted;
        using (var aes = Aes.Create())
        {
            aes.Key = EncryptionKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            encrypted = encryptor.TransformFinalBlock(compressed, 0, compressed.Length);
        }

        byte[] combined = new byte[iv.Length + encrypted.Length];
        Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
        Buffer.BlockCopy(encrypted, 0, combined, iv.Length, encrypted.Length);

        string base64 = Convert.ToBase64String(combined)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return $"CL-{base64}";
    }

    public static bool TryParseRoomCode(string roomCode, out P2PRoomPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(roomCode)) return false;

        string cleaned = roomCode.Trim();
        if (cleaned.StartsWith("clegendary://join/", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned.Substring("clegendary://join/".Length);
        }

        if (cleaned.StartsWith("CL-", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned.Substring(3);
        }

        string incoming = cleaned.Replace('-', '+').Replace('_', '/');
        int pad = incoming.Length % 4;
        if (pad == 2) incoming += "==";
        else if (pad == 3) incoming += "=";

        try
        {
            byte[] combined = Convert.FromBase64String(incoming);
            if (combined.Length <= 16) return false;

            byte[] iv = new byte[16];
            Buffer.BlockCopy(combined, 0, iv, 0, 16);

            int cipherLength = combined.Length - 16;
            byte[] cipherText = new byte[cipherLength];
            Buffer.BlockCopy(combined, 16, cipherText, 0, cipherLength);

            byte[] decrypted;
            using (var aes = Aes.Create())
            {
                aes.Key = EncryptionKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                decrypted = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
            }

            using var inMs = new MemoryStream(decrypted);
            using var gzip = new GZipStream(inMs, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gzip.CopyTo(outMs);

            string json = Encoding.UTF8.GetString(outMs.ToArray());
            payload = JsonConvert.DeserializeObject<P2PRoomPayload>(json);
            return payload != null && !string.IsNullOrEmpty(payload.HostAddress) && payload.Port > 0;
        }
        catch
        {
            return false;
        }
    }
}
