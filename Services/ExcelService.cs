using ClosedXML.Excel;
using Microsoft.Win32;
using PersonelTakip.Models;
using System.IO;

namespace PersonelTakip.Services;

public class ExcelService
{
    private readonly DatabaseService _databaseService;

    public ExcelService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public void TemplateIndir()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = "Personel_Sablon.xlsx"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Personeller");
                SetTemplateHeaders(worksheet);
                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(saveFileDialog.FileName);
            }
        }
    }

    public async Task<string> PersonelleriDisariAktarAsync(List<Personel> personeller)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"Personel_Listesi_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Personeller");
                SetHeaders(worksheet);

                int row = 2;
                foreach (var p in personeller)
                {
                    worksheet.Cell(row, 1).Value = p.Id.ToString();
                    worksheet.Cell(row, 2).Value = p.AdiSoyadi;
                    worksheet.Cell(row, 3).Value = p.SantiyeKod;
                    worksheet.Cell(row, 4).Value = p.BolumuDisplay;
                    worksheet.Cell(row, 5).Value = p.GoreviDisplay;
                    worksheet.Cell(row, 6).Value = p.UyruguDisplay;
                    worksheet.Cell(row, 7).Value = p.IseGirisTarihi?.ToString("dd.MM.yyyy");
                    worksheet.Cell(row, 8).Value = p.TelefonNumarasi;
                    worksheet.Cell(row, 9).Value = (double)(p.Maas ?? 0);
                    worksheet.Cell(row, 10).Value = p.ParaBirimiDisplay;
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(saveFileDialog.FileName);
                return saveFileDialog.FileName;
            }
        }
        return string.Empty;
    }

    public async Task<(List<Personel> personeller, string error)> ExceldenOkuAsync(bool isUpdate)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx"
        };

        if (openFileDialog.ShowDialog() != true) return (new(), "Dosya seçilmedi.");

        try
        {
            using (var workbook = new XLWorkbook(openFileDialog.FileName))
            {
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Header'ı atla

                var santiyeler = await _databaseService.SantiyeleriGetirAsync();
                var bolumler = await _databaseService.LookupGetirAsync("bolumler");
                var gorevler = await _databaseService.LookupGetirAsync("gorevler");
                var uyruklar = await _databaseService.LookupGetirAsync("uyruklar");
                var paraBirimleri = await _databaseService.LookupGetirAsync("para_birimleri");

                var list = new List<Personel>();

                foreach (var row in rows)
                {
                    var p = new Personel();
                    int startCol = 1;

                    if (isUpdate)
                    {
                        var idStr = row.Cell(1).GetValue<string>();
                        if (Guid.TryParse(idStr, out Guid id)) p.Id = id;
                        else continue;
                        startCol = 2;
                    }

                    p.AdiSoyadi = row.Cell(startCol).GetValue<string>();
                    if (string.IsNullOrWhiteSpace(p.AdiSoyadi)) continue;

                    var santiyeKod = row.Cell(startCol + 1).GetValue<string>();
                    p.SantiyeId = santiyeler.FirstOrDefault(s => s.Kod.Equals(santiyeKod, StringComparison.OrdinalIgnoreCase))?.Id;

                    var bolumAdi = row.Cell(startCol + 2).GetValue<string>();
                    p.Bolumu = bolumler.FirstOrDefault(b => b.Adi.Equals(bolumAdi, StringComparison.OrdinalIgnoreCase))?.Id;

                    var gorevAdi = row.Cell(startCol + 3).GetValue<string>();
                    p.Gorevi = gorevler.FirstOrDefault(g => g.Adi.Equals(gorevAdi, StringComparison.OrdinalIgnoreCase))?.Id;

                    var uyrukAdi = row.Cell(startCol + 4).GetValue<string>();
                    p.Uyrugu = uyruklar.FirstOrDefault(u => u.Adi.Equals(uyrukAdi, StringComparison.OrdinalIgnoreCase))?.Id;

                    var tarihStr = row.Cell(startCol + 5).GetValue<string>();
                    if (DateTime.TryParse(tarihStr, out DateTime tarih)) p.IseGirisTarihi = tarih;

                    p.TelefonNumarasi = row.Cell(startCol + 6).GetValue<string>();
                    
                    var maasVal = row.Cell(startCol + 7).GetValue<string>();
                    if (decimal.TryParse(maasVal, out decimal maas)) p.Maas = maas;

                    var pbAdi = row.Cell(startCol + 8).GetValue<string>();
                    p.ParaBirimi = paraBirimleri.FirstOrDefault(pb => pb.Adi.Equals(pbAdi, StringComparison.OrdinalIgnoreCase))?.Id;

                    list.Add(p);
                }

                return (list, string.Empty);
            }
        }
        catch (Exception ex)
        {
            return (new(), ex.Message);
        }
    }

    private void SetHeaders(IXLWorksheet worksheet)
    {
        worksheet.Cell(1, 1).Value = "ID (Güncelleme İçin)";
        worksheet.Cell(1, 2).Value = "Adı Soyadı";
        worksheet.Cell(1, 3).Value = "Şantiye Kodu";
        worksheet.Cell(1, 4).Value = "Bölüm";
        worksheet.Cell(1, 5).Value = "Görev";
        worksheet.Cell(1, 6).Value = "Uyruk";
        worksheet.Cell(1, 7).Value = "İşe Giriş Tarihi";
        worksheet.Cell(1, 8).Value = "Telefon";
        worksheet.Cell(1, 9).Value = "Maaş";
        worksheet.Cell(1, 10).Value = "Para Birimi";

        var headerRange = worksheet.Range(1, 1, 1, 10);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
    }

    private void SetTemplateHeaders(IXLWorksheet worksheet)
    {
        worksheet.Cell(1, 1).Value = "Adı Soyadı";
        worksheet.Cell(1, 2).Value = "Şantiye Kodu";
        worksheet.Cell(1, 3).Value = "Bölüm";
        worksheet.Cell(1, 4).Value = "Görev";
        worksheet.Cell(1, 5).Value = "Uyruk";
        worksheet.Cell(1, 6).Value = "İşe Giriş Tarihi";
        worksheet.Cell(1, 7).Value = "Telefon";
        worksheet.Cell(1, 8).Value = "Maaş";
        worksheet.Cell(1, 9).Value = "Para Birimi";

        var headerRange = worksheet.Range(1, 1, 1, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
    }
}
