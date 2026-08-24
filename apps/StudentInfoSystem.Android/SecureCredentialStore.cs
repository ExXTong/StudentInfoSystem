using Android.Content;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Android.Security.Keystore;

namespace StudentInfoSystem.Android;

public class SecureCredentialStore
{
    private const string KeyName = "sis_credential_key";
    private const string PrefName = "secure_credentials";
    private readonly Context _context;
    private readonly ISharedPreferences _prefs;

    public SecureCredentialStore(Context context)
    {
        _context = context;
        _prefs = context.GetSharedPreferences(PrefName, FileCreationMode.Private)!;
    }

    public void SavePassword(string username, string password)
    {
        var key = GetOrCreateKey();
        var iv = RandomNumberGenerator.GetBytes(12);

        var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
        cipher.Init(Javax.Crypto.CipherMode.EncryptMode, key, new GCMParameterSpec(128, iv));

        var plain = Encoding.UTF8.GetBytes($"{username}\n{password}");
        var encrypted = cipher.DoFinal(plain)!;

        using var ms = new MemoryStream();
        ms.Write(iv, 0, iv.Length);
        ms.Write(encrypted, 0, encrypted.Length);
        var data = Convert.ToBase64String(ms.ToArray());
        _prefs.Edit()!.PutString("data", data)!.Apply();
    }

    public (string Username, string Password)? LoadPassword()
    {
        var data = _prefs.GetString("data", null);
        if (string.IsNullOrEmpty(data)) return null;

        try
        {
            var key = GetOrCreateKey();
            var bytes = Convert.FromBase64String(data!);
            var iv = new byte[12];
            Array.Copy(bytes, 0, iv, 0, 12);
            var encrypted = new byte[bytes.Length - 12];
            Array.Copy(bytes, 12, encrypted, 0, encrypted.Length);

            var cipher = Cipher.GetInstance("AES/GCM/NoPadding")!;
            cipher.Init(Javax.Crypto.CipherMode.DecryptMode, key, new GCMParameterSpec(128, iv));
            var plain = cipher.DoFinal(encrypted)!;
            var text = Encoding.UTF8.GetString(plain);
            var idx = text.IndexOf('\n');
            if (idx <= 0) return null;
            return (text[..idx], text[(idx + 1)..]);
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        _prefs.Edit()!.Remove("data")!.Apply();
    }

    private ISecretKey GetOrCreateKey()
    {
        var ks = KeyStore.GetInstance("AndroidKeyStore")!;
        ks.Load(null);
        var existing = ks.GetKey(KeyName, null) as ISecretKey;
        if (existing != null) return existing;

        // API36 绑定的 Builder 构造参数为 KeyStorePurpose 枚举
        var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;
        generator.Init(new KeyGenParameterSpec.Builder(KeyName,
                KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .Build());
        return (ISecretKey)generator.GenerateKey()!;
    }
}
