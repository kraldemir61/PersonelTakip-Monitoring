using ClosedXML.Excel;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PersonelTakip.Services
{
    public class RaporService
    {
        private readonly string _connectionString;
        private readonly Dictionary<string, string> _moduleQueries;

        public RaporService(string connectionString)
        {
            _connectionString = connectionString;
            _moduleQueries = new Dictionary<string, string>
            {
                ["Personel"] = @"
                    SELECT p.adi_soyadi AS ""Ad Soyad"", 
                           s.adi AS ""Şantiye"",
                           b.adi AS ""Bölüm"", 
                           g.adi AS ""Görev"", 
                           u.adi AS ""Uyruk"",
                           p.maas AS ""Maaş"", 
                           pb.adi AS ""Para Birimi"",
                           p.ise_giris_tarihi AS ""İşe Giriş"", 
                           p.telefon_numarasi AS ""Telefon"",
                           CASE p.aktif WHEN true THEN 'Aktif' ELSE 'Pasif' END AS ""Durum""
                    FROM personeller p
                    LEFT JOIN santiyeler s ON p.santiye_id = s.id
                    LEFT JOIN bolumler b ON p.bolumu = b.id
                    LEFT JOIN gorevler g ON p.gorevi = g.id
                    LEFT JOIN uyruklar u ON p.uyrugu = u.id
                    LEFT JOIN para_birimleri pb ON p.para_birimi = pb.id
                    ORDER BY p.adi_soyadi",

                ["Cihazlar"] = @"
                    SELECT c.cihaz_adi AS ""Cihaz Adı"", 
                           c.seri_no AS ""Seri No"", 
                           c.marka AS ""Marka"", 
                           c.model AS ""Model"",
                           s.adi AS ""Şantiye"",
                           p.adi_soyadi AS ""Zimmetli Personel"",
                           c.ozellik AS ""Özellik"",
                           CASE c.tur 
                                WHEN 0 THEN 'Ölçüm Cihazı' 
                                WHEN 1 THEN 'Ofis Cihazı' 
                                ELSE 'Bilinmeyen' 
                           END AS ""Tür"",
                           c.durum AS ""Durum""
                    FROM cihazlar c
                    LEFT JOIN santiyeler s ON c.santiye_id = s.id
                    LEFT JOIN personeller p ON c.zimmetli_personel_id = p.id
                    ORDER BY c.cihaz_adi",

                ["ZimmetGecmisi"] = @"
                    SELECT c.seri_no AS ""Cihaz Seri No"", 
                           c.cihaz_adi AS ""Cihaz Adı"",
                           p.adi_soyadi AS ""Personel"",
                           czg.zimmet_tarihi AS ""Zimmet Tarihi"",
                           czg.iade_tarihi AS ""İade Tarihi"",
                           czg.aciklama AS ""Açıklama""
                    FROM cihaz_zimmet_gecmisi czg
                    LEFT JOIN cihazlar c ON czg.cihaz_id = c.id
                    LEFT JOIN personeller p ON czg.personel_id = p.id
                    ORDER BY czg.zimmet_tarihi DESC",

                ["SantiyeDagilimi"] = @"
                    SELECT s.adi AS ""Şantiye Adı"", 
                           s.kod AS ""Şantiye Kodu"",
                           (SELECT COUNT(*) FROM personeller p WHERE p.santiye_id = s.id AND p.aktif = true) AS ""Aktif Personel"",
                           (SELECT COUNT(*) FROM cihazlar c WHERE c.santiye_id = s.id) AS ""Cihaz Sayısı"",
                           s.adres AS ""Adres"", 
                           s.telefon AS ""Telefon""
                    FROM santiyeler s
                    WHERE s.aktif = true
                    ORDER BY s.adi"
            };
        }

        public Dictionary<string, string> GetModuleDisplayNames()
        {
            return new Dictionary<string, string>
            {
                ["Personel"] = "Personel Listesi",
                ["Cihazlar"] = "Cihaz Envanteri",
                ["ZimmetGecmisi"] = "Zimmet/Hareket Geçmişi",
                ["SantiyeDagilimi"] = "Şantiye Dağılım Raporu"
            };
        }

        public async Task<DataTable> GetReportAsync(string moduleName)
        {
            if (!_moduleQueries.ContainsKey(moduleName))
                throw new ArgumentException($"Bilinmeyen modül: {moduleName}");

            try
            {
                var dt = new DataTable();
                using var conn = new NpgsqlConnection(_connectionString);
                // Bağlantı zaman aşımını kısa tutalım
                var csb = new NpgsqlConnectionStringBuilder(_connectionString) { Timeout = 5 };
                conn.ConnectionString = csb.ToString();

                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(_moduleQueries[moduleName], conn);
                using var reader = await cmd.ExecuteReaderAsync();

                // Sütunları tanımla
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string colName = reader.GetName(i);
                    var fieldType = reader.GetFieldType(i);
                    dt.Columns.Add(colName, fieldType ?? typeof(string));
                }

                // Verileri oku
                while (await reader.ReadAsync())
                {
                    var row = dt.NewRow();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                    }
                    dt.Rows.Add(row);
                }

                return dt;
            }
            catch (NpgsqlException ex) when (ex.InnerException is System.Net.Sockets.SocketException sx && sx.SocketErrorCode == System.Net.Sockets.SocketError.HostNotFound)
            {
                throw new Exception("Sunucu adresi bulunamadı. Lütfen bağlantı ayarlarındaki Host (IP) bilgisini kontrol edin.");
            }
            catch (NpgsqlException ex)
            {
                throw new Exception($"Veritabanı hatası: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Rapor yükleme hatası: {ex.Message}");
            }
        }

        public void ExportToExcel(DataTable data, string filePath, string sheetName)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(sheetName.Length > 31 ? sheetName.Substring(0, 31) : sheetName);

            // Başlıklar
            for (int col = 0; col < data.Columns.Count; col++)
            {
                var cell = ws.Cell(1, col + 1);
                cell.Value = data.Columns[col].ColumnName;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Veriler
            for (int row = 0; row < data.Rows.Count; row++)
            {
                for (int col = 0; col < data.Columns.Count; col++)
                {
                    var cell = ws.Cell(row + 2, col + 1);
                    var val = data.Rows[row][col];
                    
                    if (val != DBNull.Value)
                    {
                        if (val is DateTime dt) cell.Value = dt;
                        else if (val is decimal dec) cell.Value = (double)dec;
                        else if (val is double d) cell.Value = d;
                        else if (val is int i) cell.Value = i;
                        else cell.Value = val.ToString();
                    }

                    if (row % 2 == 1) cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
                }
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);
            workbook.SaveAs(filePath);
        }
    }
}
