namespace StudentInfoSystem.Core.Security;

public interface ISecureCredentialStore
{
    void SavePassword(string username, string password);
    (string Username, string Password)? LoadPassword();
    void Clear();
}
