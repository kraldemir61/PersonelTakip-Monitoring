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
            FileName = $"Personel_Listesi_{DateTime.Now:dd.MM.yyyy HH.mm.ss}.xlsx"
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
                    p.BolumuDisplay = bolumAdi;
                    p.Bolumu = bolumler.FirstOrDefault(b => b.Adi.Equals(bolumAdi, StringComparison.OrdinalIgnoreCase))?.Id;

                    var gorevAdi = row.Cell(startCol + 3).GetValue<string>();
                    p.GoreviDisplay = gorevAdi;
                    p.Gorevi = gorevler.FirstOrDefault(g => g.Adi.Equals(gorevAdi, StringComparison.OrdinalIgnoreCase))?.Id;

                    var uyrukAdi = row.Cell(startCol + 4).GetValue<string>();
                    p.UyruguDisplay = uyrukAdi;
                    p.Uyrugu = uyruklar.FirstOrDefault(u => u.Adi.Equals(uyrukAdi, StringComparison.OrdinalIgnoreCase))?.Id;

                    var tarihStr = row.Cell(startCol + 5).GetValue<string>();
                    if (DateTime.TryParse(tarihStr, out DateTime tarih)) p.IseGirisTarihi = tarih;

                    p.TelefonNumarasi = row.Cell(startCol + 6).GetValue<string>();
                    
                    var maasVal = row.Cell(startCol + 7).GetValue<string>();
                    if (decimal.TryParse(maasVal, out decimal maas)) p.Maas = maas;

                    var pbAdi = row.Cell(startCol + 8).GetValue<string>();
                    p.ParaBirimiDisplay = pbAdi;
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

    #region Cihaz Excel İşlemleri

    public void CihazTemplateIndir()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = "Cihaz_Sablon.xlsx"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Cihazlar");
                SetCihazTemplateHeaders(worksheet);
                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(saveFileDialog.FileName);
            }
        }
    }

    public async Task<string> CihazlariDisariAktarAsync(List<Cihaz> cihazlar)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"Cihaz_Listesi_{DateTime.Now:dd.MM.yyyy HH.mm.ss}.xlsx"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Cihazlar");
                SetCihazHeaders(worksheet, true);

                int row = 2;
                foreach (var c in cihazlar)
                {
                    worksheet.Cell(row, 1).Value = c.Id.ToString();
                    worksheet.Cell(row, 2).Value = c.SeriNo;
                    worksheet.Cell(row, 3).Value = c.CihazAdi;
                    worksheet.Cell(row, 4).Value = c.Marka;
                    worksheet.Cell(row, 5).Value = c.Model;
                    worksheet.Cell(row, 6).Value = c.SahipFirma;
                    worksheet.Cell(row, 7).Value = c.SantiyeKod;
                    worksheet.Cell(row, 8).Value = c.Not;
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(saveFileDialog.FileName);
                return saveFileDialog.FileName;
            }
        }
        return string.Empty;
    }

    public async Task<(List<Cihaz> cihazlar, string error)> CihazExceldenOkuAsync(bool isUpdate)
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
                var rows = worksheet.RangeUsed().RowsUsed().Skip(1);

                var santiyeler = await _databaseService.SantiyeleriGetirAsync();
                var list = new List<Cihaz>();

                foreach (var row in rows)
                {
                    var c = new Cihaz { Tur = CihazTuru.Olcum, Durum = "Boşta", SonIslemTarihi = DateTime.Now };
                    int startCol = 1;

                    if (isUpdate)
                    {
                        var idStr = row.Cell(1).GetValue<string>();
                        if (Guid.TryParse(idStr, out Guid id)) c.Id = id;
                        else continue;
                        startCol = 2;
                    }
                    else
                    {
                        c.Id = Guid.NewGuid();
                    }

                    c.SeriNo = row.Cell(startCol).GetValue<string>();
                    if (string.IsNullOrWhiteSpace(c.SeriNo)) continue;

                    c.CihazAdi = row.Cell(startCol + 1).GetValue<string>();
                    c.Marka = row.Cell(startCol + 2).GetValue<string>();
                    c.Model = row.Cell(startCol + 3).GetValue<string>();
                    c.SahipFirma = row.Cell(startCol + 4).GetValue<string>();
                    
                    var santiyeKod = row.Cell(startCol + 5).GetValue<string>();
                    var sId = santiyeler.FirstOrDefault(s => s.Kod.Equals(santiyeKod, StringComparison.OrdinalIgnoreCase))?.Id;
                    c.SantiyeId = sId;
                    if (sId.HasValue) c.Durum = "Şantiyede";

                    c.Not = row.Cell(startCol + 6).GetValue<string>();

                    list.Add(c);
                }

                return (list, string.Empty);
            }
        }
        catch (Exception ex)
        {
            return (new(), ex.Message);
        }
    }

    private void SetCihazHeaders(IXLWorksheet worksheet, bool includeId)
    {
        int col = 1;
        if (includeId) worksheet.Cell(1, col++).Value = "ID (Güncelleme İçin)";
        worksheet.Cell(1, col++).Value = "Seri No";
        worksheet.Cell(1, col++).Value = "Cihaz Adı";
        worksheet.Cell(1, col++).Value = "Marka";
        worksheet.Cell(1, col++).Value = "Model";
        worksheet.Cell(1, col++).Value = "Sahip Firma";
        worksheet.Cell(1, col++).Value = "Şantiye Kodu";
        worksheet.Cell(1, col++).Value = "Not";

        var headerRange = worksheet.Range(1, 1, 1, col - 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
    }

    private void SetCihazTemplateHeaders(IXLWorksheet worksheet)
    {
        worksheet.Cell(1, 1).Value = "Seri No";
        worksheet.Cell(1, 2).Value = "Cihaz Adı";
        worksheet.Cell(1, 3).Value = "Marka";
        worksheet.Cell(1, 4).Value = "Model";
        worksheet.Cell(1, 5).Value = "Sahip Firma";
        worksheet.Cell(1, 6).Value = "Şantiye Kodu";
        worksheet.Cell(1, 7).Value = "Not";

        var headerRange = worksheet.Range(1, 1, 1, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
    }

    public async Task<string> CihazHareketleriDisariAktarAsync(List<CihazHareket> hareketler)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"Cihaz_Hareketleri_{DateTime.Now:dd.MM.yyyy HH.mm.ss}.xlsx"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Hareketler");
                
                // Headers
                worksheet.Cell(1, 1).Value = "Tarih";
                worksheet.Cell(1, 2).Value = "Seri No";
                worksheet.Cell(1, 3).Value = "Cihaz Adı";
                worksheet.Cell(1, 4).Value = "İşlem Türü";
                worksheet.Cell(1, 5).Value = "Nereden";
                worksheet.Cell(1, 6).Value = "Nereye";
                worksheet.Cell(1, 7).Value = "Açıklama";
                worksheet.Cell(1, 8).Value = "İşlemi Yapan";

                var headerRange = worksheet.Range(1, 1, 1, 8);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

                int row = 2;
                foreach (var h in hareketler)
                {
                    worksheet.Cell(row, 1).Value = h.Tarih.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cell(row, 2).Value = h.CihazSeriNo;
                    worksheet.Cell(row, 3).Value = h.CihazAdi;
                    worksheet.Cell(row, 4).Value = h.IslemTuru;
                    worksheet.Cell(row, 5).Value = h.NeredenSantiyeAdi;
                    worksheet.Cell(row, 6).Value = h.NereyeSantiyeAdi;
                    worksheet.Cell(row, 7).Value = h.Aciklama;
                    worksheet.Cell(row, 8).Value = h.KullaniciAdi;
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(saveFileDialog.FileName);
                return saveFileDialog.FileName;
            }
        }
        return string.Empty;
    }

    #endregion
}
