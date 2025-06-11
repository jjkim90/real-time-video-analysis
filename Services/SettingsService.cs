using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using RealTimeVideoAnalysis.Models;

namespace RealTimeVideoAnalysis.Services
{
    public interface ISettingsService
    {
        Task<bool> SaveSettingsAsync(AppSettings settings, string filePath);
        Task<AppSettings> LoadSettingsAsync(string filePath);
    }

    public class SettingsService : ISettingsService
    {
        private readonly JsonSerializerOptions _jsonOptions;

        public SettingsService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<bool> SaveSettingsAsync(AppSettings settings, string filePath)
        {
            try
            {
                settings.SavedAt = DateTime.Now;
                string json = JsonSerializer.Serialize(settings, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<AppSettings> LoadSettingsAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                string json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}