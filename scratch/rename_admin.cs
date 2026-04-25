using Dapper;
using Npgsql;
using System;

string connString = "Host=localhost;Database=personel_takip;Username=postgres;Password=admin";

try {
    using var conn = new NpgsqlConnection(connString);
    conn.Open();
    
    // Önce kontrol et
    var user = conn.QueryFirstOrDefault("SELECT * FROM kullanicilar WHERE kullanici_adi = 'Roujin61'");
    if (user != null) {
        Console.WriteLine("Roujin61 kullanıcısı bulundu. Güncelleniyor...");
        int affected = conn.Execute("UPDATE kullanicilar SET kullanici_adi = 'Admin' WHERE kullanici_adi = 'Roujin61'");
        Console.WriteLine($"{affected} satır güncellendi.");
    } else {
        Console.WriteLine("Roujin61 kullanıcısı bulunamadı.");
    }
} catch (Exception ex) {
    Console.WriteLine($"Hata: {ex.Message}");
}
