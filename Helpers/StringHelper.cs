using System;

namespace PersonelTakip.Monitoring.Helpers;

public static class StringHelper
{
    public static string NormalizeTurkish(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        
        // Küçük harfe çevir (Türkçe kültürüne uygun)
        string lower = text.ToLower(new System.Globalization.CultureInfo("tr-TR"));
        
        return lower
               .Replace("ı", "i")
               .Replace("ğ", "g")
               .Replace("ü", "u")
               .Replace("ş", "s")
               .Replace("ö", "o")
               .Replace("ç", "c")
               .Trim();
    }

    public static bool SmartSearch(string source, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        if (string.IsNullOrWhiteSpace(source)) return false;

        var normalizedSource = NormalizeTurkish(source);
        var searchTerms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var term in searchTerms)
        {
            var normalizedTerm = NormalizeTurkish(term);
            if (!normalizedSource.Contains(normalizedTerm))
                return false; // Tüm parçalar bulunmalı (AND mantığı)
        }
        return true;
    }
}
