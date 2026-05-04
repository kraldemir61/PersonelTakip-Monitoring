using Microsoft.Data.Sqlite;
using PersonelTakip.Monitoring.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PersonelTakip.Monitoring.Services;

public class LocalNotificationService
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public LocalNotificationService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "PersonelTakipMonitoring");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        
        _dbPath = Path.Combine(folder, "notifications_archive.db");
        _connectionString = $"Data Source={_dbPath}";
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS local_bildirimler (
                id INTEGER PRIMARY KEY,
                original_id INTEGER UNIQUE,
                mesaj TEXT,
                tarih DATETIME,
                okundu_mu BOOLEAN DEFAULT 0,
                hedef_santiye_id TEXT
            );
            
            -- Mükerrer kayıtları temizle (Eğer daha önceden oluştularsa)
            DELETE FROM local_bildirimler 
            WHERE id NOT IN (
                SELECT MIN(id) 
                FROM local_bildirimler 
                GROUP BY original_id
            );";
        cmd.ExecuteNonQuery();

        // Migration: Eğer tablo varsa ama sütun yoksa ekle
        try {
            cmd.CommandText = "ALTER TABLE local_bildirimler ADD COLUMN hedef_santiye_id TEXT;";
            cmd.ExecuteNonQuery();
        } catch { }
    }

    public async Task<List<Bildirim>> GetLocalBildirimlerAsync()
    {
        var list = new List<Bildirim>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT original_id, mesaj, tarih, okundu_mu, hedef_santiye_id FROM local_bildirimler ORDER BY tarih DESC";
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Guid? targetId = null;
            if (!reader.IsDBNull(4))
            {
                if (Guid.TryParse(reader.GetString(4), out var g)) targetId = g;
            }

            list.Add(new Bildirim
            {
                Id = reader.GetInt32(0),
                Mesaj = reader.GetString(1),
                Tarih = reader.GetDateTime(2),
                OkunduMu = reader.GetBoolean(3),
                HedefSantiyeId = targetId
            });
        }
        return list;
    }

    public async Task SaveBildirimAsync(Bildirim b)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO local_bildirimler (original_id, mesaj, tarih, okundu_mu, hedef_santiye_id)
            VALUES (@Id, @Msg, @Date, @Read, @Target)";
        cmd.Parameters.AddWithValue("@Id", b.Id);
        cmd.Parameters.AddWithValue("@Msg", b.Mesaj);
        cmd.Parameters.AddWithValue("@Date", b.Tarih);
        cmd.Parameters.AddWithValue("@Read", b.OkunduMu);
        cmd.Parameters.AddWithValue("@Target", b.HedefSantiyeId?.ToString() ?? (object)DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkAsReadAsync(int bildirimId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE local_bildirimler SET okundu_mu = 1 WHERE original_id = @Id";
        cmd.Parameters.AddWithValue("@Id", bildirimId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteBildirimAsync(int bildirimId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM local_bildirimler WHERE original_id = @Id";
        cmd.Parameters.AddWithValue("@Id", bildirimId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkAllAsReadAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE local_bildirimler SET okundu_mu = 1";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearAllAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM local_bildirimler";
        await cmd.ExecuteNonQueryAsync();
    }
}
