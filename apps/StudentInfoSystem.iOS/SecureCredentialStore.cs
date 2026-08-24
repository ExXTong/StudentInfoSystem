using Foundation;
using Security;
using System;
using System.Text;

namespace StudentInfoSystem.iOS;

public class SecureCredentialStore
{
    private const string Service = "com.example.studentinfosystem";
    private readonly string _account = "default";

    public void SavePassword(string username, string password)
    {
        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = Service,
            Account = _account,
            ValueData = NSData.FromString($"{username}\n{password}", NSStringEncoding.UTF8)
        };
        SecKeyChain.Remove(record);
        SecKeyChain.Add(record);
    }

    public (string Username, string Password)? LoadPassword()
    {
        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = Service,
            Account = _account
        };
        SecStatusCode status;
        var match = SecKeyChain.QueryAsRecord(record, out status);
        if (status != SecStatusCode.Success || match?.ValueData == null)
        {
            return null;
        }

        var text = NSString.FromData(match.ValueData, NSStringEncoding.UTF8)?.ToString();
        if (string.IsNullOrEmpty(text)) return null;
        var idx = text.IndexOf('\n');
        if (idx <= 0) return null;
        return (text[..idx], text[(idx + 1)..]);
    }

    public void Clear()
    {
        var record = new SecRecord(SecKind.GenericPassword)
        {
            Service = Service,
            Account = _account
        };
        SecKeyChain.Remove(record);
    }
}
