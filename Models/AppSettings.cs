using System;
using System.Text.Json.Serialization;

namespace RealTimeVideoAnalysis.Models
{
    public class AppSettings
    {
        [JsonPropertyName("roiSettings")]
        public RoiSettings RoiSettings { get; set; }

        [JsonPropertyName("imageEffectSettings")]
        public ImageEffectSettings ImageEffectSettings { get; set; }

        [JsonPropertyName("adjustmentSettings")]
        public AdjustmentSettings AdjustmentSettings { get; set; }

        [JsonPropertyName("colorDetectionSettings")]
        public ColorDetectionSettings ColorDetectionSettings { get; set; }

        [JsonPropertyName("savedAt")]
        public DateTime SavedAt { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        public AppSettings()
        {
            RoiSettings = new RoiSettings();
            ImageEffectSettings = new ImageEffectSettings();
            AdjustmentSettings = new AdjustmentSettings();
            ColorDetectionSettings = new ColorDetectionSettings();
            SavedAt = DateTime.Now;
            Version = "1.0";
        }
    }

    public class RoiSettings
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("isDefined")]
        public bool IsDefined { get; set; }
    }

    public class ImageEffectSettings
    {
        [JsonPropertyName("currentEffect")]
        public string CurrentEffect { get; set; }

        [JsonPropertyName("binaryThreshold")]
        public double BinaryThreshold { get; set; }

        [JsonPropertyName("blurStrength")]
        public double BlurStrength { get; set; }

        [JsonPropertyName("sharpenStrength")]
        public double SharpenStrength { get; set; }
    }

    public class AdjustmentSettings
    {
        [JsonPropertyName("brightness")]
        public double Brightness { get; set; }

        [JsonPropertyName("contrast")]
        public double Contrast { get; set; }

        [JsonPropertyName("targetFps")]
        public int TargetFps { get; set; }
    }

    public class ColorDetectionSettings
    {
        [JsonPropertyName("hueLower")]
        public double HueLower { get; set; }

        [JsonPropertyName("hueUpper")]
        public double HueUpper { get; set; }

        [JsonPropertyName("saturationLower")]
        public double SaturationLower { get; set; }

        [JsonPropertyName("saturationUpper")]
        public double SaturationUpper { get; set; }

        [JsonPropertyName("valueLower")]
        public double ValueLower { get; set; }

        [JsonPropertyName("valueUpper")]
        public double ValueUpper { get; set; }
    }
}