using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using StudentInfoSystem.Core.Models;

namespace StudentInfoSystem.Core.Storage;

/// <summary>
/// 本地 SQLite 存储，用于保存成绩、学生信息等离线数据。
/// </summary>
public class LocalDataStore
{
    private readonly string _connectionString;

    public LocalDataStore(string? databasePath = null)
    {
        var dir = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StudentInfoSystem");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "studentinfo.db");
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS user_data (
                username TEXT NOT NULL,
                key TEXT NOT NULL,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                PRIMARY KEY(username, key)
            );
            """;
        command.ExecuteNonQuery();
    }

    public void SaveGrades(string username, List<GradeInfo> grades)
    {
        Save(username, "grades", grades);
    }

    public List<GradeInfo>? LoadGrades(string username)
    {
        return Load<List<GradeInfo>>(username, "grades");
    }

    public void SaveStudentInfo(string username, StudentInfo info)
    {
        Save(username, "profile", info);
    }

    public StudentInfo? LoadStudentInfo(string username)
    {
        return Load<StudentInfo>(username, "profile");
    }


    public class SavedCredentials
    {
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Salt { get; set; } = "";
    }

    public void SaveCredentials(string username, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPassword(password, salt);
        Save("default", "credentials", new SavedCredentials
        {
            Username = username,
            PasswordHash = Convert.ToBase64String(hash),
            Salt = Convert.ToBase64String(salt)
        });
    }

    public SavedCredentials? LoadCredentials()
    {
        return Load<SavedCredentials>("default", "credentials");
    }

    public bool VerifyLocalCredentials(string username, string password)
    {
        var saved = LoadCredentials();
        if (saved == null
            || !string.Equals(saved.Username, username, StringComparison.Ordinal)
            || string.IsNullOrEmpty(saved.PasswordHash)
            || string.IsNullOrEmpty(saved.Salt))
        {
            return false;
        }

        var salt = Convert.FromBase64String(saved.Salt);
        var hash = HashPassword(password, salt);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(saved.PasswordHash));
    }

    private static byte[] HashPassword(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
    }

    public void ClearUserData(string username)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM user_data WHERE username=$username;";
        command.Parameters.AddWithValue("$username", username);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// 删除本地保存的凭据记录（SaveCredentials 写入的 'default' 行）。
    /// 注意 ClearUserData 按 username 删除，不会覆盖到这一行。
    /// </summary>
    public void ClearCredentials()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM user_data WHERE username='default' AND key='credentials';";
        command.ExecuteNonQuery();
    }

    private void Save<T>(string username, string key, T data)
    {
        var json = JsonSerializer.Serialize(data);
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO user_data(username, key, json, updated_at)
            VALUES($username, $key, $json, $time)
            ON CONFLICT(username, key) DO UPDATE SET json=$json, updated_at=$time;
            """;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$json", json);
        command.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private T? Load<T>(string username, string key)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM user_data WHERE username=$username AND key=$key;";
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$key", key);
        var result = command.ExecuteScalar() as string;
        return result == null ? default : JsonSerializer.Deserialize<T>(result);
    }
}
