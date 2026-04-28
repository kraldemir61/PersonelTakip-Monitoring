using System;
using System.IO;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace PersonelTakip.Services
{
    public static class LicenseManager
    {
        private static readonly string GIZLI_ANAHTAR = "WpfPersonelVeZimmetTakibi61@@!!";
        private static string AppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersonelTakip");
        private static string LicenseFilePath => Path.Combine(AppDataPath, "license.dat");
        private static string DemoFilePath => Path.Combine(AppDataPath, "demo.dat");
        
        // Obfuscated Registry Keys
        private const string REG_PATH = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\ShellExtensions";
        private const string REG_KEY = "User_License_Token";

        public static string GetHardwareId()
        {
            string cpuInfo = string.Empty;
            try
            {
                ManagementClass mc = new ManagementClass("win32_processor");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    if (cpuInfo == "")
                    {
                        cpuInfo = mo.Properties["processorID"].Value?.ToString() ?? "";
                        break;
                    }
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(cpuInfo))
                cpuInfo = Environment.MachineName;

            return cpuInfo;
        }

        public static string GenerateLicenseKey(string hardwareId)
        {
            string rawData = hardwareId + GIZLI_ANAHTAR;
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                string hex = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);
                return string.Format("{0}-{1}-{2}-{3}",
                    hex.Substring(0, 4), hex.Substring(4, 4), hex.Substring(8, 4), hex.Substring(12, 4));
            }
        }

        public static bool ValidateLicense(string inputKey)
        {
            if (string.IsNullOrWhiteSpace(inputKey)) return false;
            string hardwareId = GetHardwareId();
            string correctKey = GenerateLicenseKey(hardwareId);
            return inputKey.Trim().ToUpper() == correctKey.ToUpper();
        }

        public static async Task SaveLicenseAsync(string key)
        {
            try
            {
                if (!Directory.Exists(AppDataPath))
                    Directory.CreateDirectory(AppDataPath);
                
                // 1. File
                File.WriteAllText(LicenseFilePath, key.Trim());
                
                // 2. Registry
                SaveToRegistry(key.Trim());
                
                // 3. Database
                var db = new DatabaseService();
                await db.SaveLicenseDataAsync(GetHardwareId(), key.Trim(), null).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lisans kaydedilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool? _isLicensedCache;
        public static async Task<bool> IsLicensedAsync()
        {
            if (_isLicensedCache.HasValue) return _isLicensedCache.Value;

            string hardwareId = GetHardwareId();
            
            // Check local file
            string? fileKey = File.Exists(LicenseFilePath) ? File.ReadAllText(LicenseFilePath) : null;
            
            // Check registry
            string? regKey = ReadFromRegistry();
            
            // Check Database
            var db = new DatabaseService();
            var (dbKey, _) = await db.GetLicenseDataAsync(hardwareId).ConfigureAwait(false);

            // Auto-Healing
            string? masterKey = null;
            if (ValidateLicense(fileKey ?? "")) masterKey = fileKey;
            else if (ValidateLicense(regKey ?? "")) masterKey = regKey;
            else if (ValidateLicense(dbKey ?? "")) masterKey = dbKey;

            if (masterKey != null)
            {
                // Restore others
                if (fileKey != masterKey) 
                {
                    if (!Directory.Exists(AppDataPath)) Directory.CreateDirectory(AppDataPath);
                    File.WriteAllText(LicenseFilePath, masterKey);
                }
                if (regKey != masterKey) SaveToRegistry(masterKey);
                if (dbKey != masterKey) await db.SaveLicenseDataAsync(hardwareId, masterKey, null).ConfigureAwait(false);
                
                _isLicensedCache = true;
                return true;
            }

            _isLicensedCache = false;
            return false;
        }

        // --- Demo System ---
        public enum DemoStatus { None, Active, Expired, RollbackDetected }
        public class DemoInfo { public DemoStatus Status; public int RemainingDays; public string Message = ""; }

        public static async Task StartDemoAsync()
        {
            if (await IsLicensedAsync().ConfigureAwait(false)) return;

            DateTime now = DateTime.Now;
            string data = $"{now:yyyy-MM-dd}|{now:yyyy-MM-dd}";
            string encrypted = Encrypt(data);

            if (!Directory.Exists(AppDataPath)) Directory.CreateDirectory(AppDataPath);
            if (!File.Exists(DemoFilePath)) File.WriteAllText(DemoFilePath, encrypted);
            
            SaveToRegistry(encrypted, true); 
            
            var db = new DatabaseService();
            await db.SaveLicenseDataAsync(GetHardwareId(), null, encrypted).ConfigureAwait(false);
        }

        public static async Task<DemoInfo> GetDemoSummaryAsync()
        {
            var info = new DemoInfo { Status = DemoStatus.None };
            if (await IsLicensedAsync().ConfigureAwait(false)) return info;

            string? fileData = File.Exists(DemoFilePath) ? File.ReadAllText(DemoFilePath) : null;
            string? regData = ReadFromRegistry(true);
            var db = new DatabaseService();
            var (_, dbData) = await db.GetLicenseDataAsync(GetHardwareId()).ConfigureAwait(false);

            string? masterData = fileData ?? regData ?? dbData;
            if (masterData == null) return info;

            try
            {
                // Healing
                if (fileData == null) { if (!Directory.Exists(AppDataPath)) Directory.CreateDirectory(AppDataPath); File.WriteAllText(DemoFilePath, masterData); }
                if (regData == null) SaveToRegistry(masterData, true);
                if (dbData == null) await db.SaveLicenseDataAsync(GetHardwareId(), null, masterData).ConfigureAwait(false);

                string decrypted = Decrypt(masterData);
                string[] parts = decrypted.Split('|');
                DateTime startDate = DateTime.Parse(parts[0]);
                DateTime lastSeenDate = DateTime.Parse(parts[1]);
                DateTime now = DateTime.Now;

                if (now < lastSeenDate)
                {
                    info.Status = DemoStatus.RollbackDetected;
                    info.Message = "Sistem saati geri alınmış! Program kilitlendi.";
                    return info;
                }

                int daysPassed = (now.Date - startDate.Date).Days;
                if (daysPassed > 7)
                {
                    info.Status = DemoStatus.Expired;
                    info.Message = "7 günlük deneme süreniz doldu.";
                }
                else
                {
                    info.Status = DemoStatus.Active;
                    info.RemainingDays = Math.Max(0, 7 - daysPassed);
                    info.Message = $"Deneme sürümü. Kalan: {info.RemainingDays} gün.";
                    
                    string updatedData = Encrypt($"{parts[0]}|{now:yyyy-MM-dd}");
                    File.WriteAllText(DemoFilePath, updatedData);
                    SaveToRegistry(updatedData, true);
                    await db.SaveLicenseDataAsync(GetHardwareId(), null, updatedData).ConfigureAwait(false);
                }
            }
            catch
            {
                info.Status = DemoStatus.RollbackDetected;
                info.Message = "Lisans verileri bozulmuş!";
            }
            return info;
        }

        private static void SaveToRegistry(string data, bool isDemo = false)
        {
            try { using var key = Registry.CurrentUser.CreateSubKey(REG_PATH); key.SetValue(isDemo ? REG_KEY + "_D" : REG_KEY, data); } catch { }
        }

        private static string? ReadFromRegistry(bool isDemo = false)
        {
            try { using var key = Registry.CurrentUser.OpenSubKey(REG_PATH); return key?.GetValue(isDemo ? REG_KEY + "_D" : REG_KEY)?.ToString(); } catch { return null; }
        }

        private static string Encrypt(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(bytes[i] ^ 0x42); 
            return Convert.ToBase64String(bytes);
        }

        private static string Decrypt(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(bytes[i] ^ 0x42);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
