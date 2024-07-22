using Extensions;
using System;
using System.Collections.Generic;
using System.IO;

namespace annotation
{
    public class BaseConfig
    {
        public class WindowCfg
        {
            public int x, y, w, h = 400;
            public string mode = "norm";
        }
        public WindowCfg Window { get; set; } = new WindowCfg();
        public float Overlap { get; set; } = 0.5f;
        public bool EditMode { get; set; }
        public string ModelPath { get; set; }
        public float ModelConfidence { get; set; } = 0.6f;

        public bool autoRect_autoSave { get; set; }
        public string ModelType { get; set; }
        public string LogDir { get; set; }
        public bool BgDetect { get; set; }
        public float Scale { get; set; } = 4f;
        public string SegModel { get; set; }

        public bool DevMode { get; set; }
        public string RecentDir { get; set; }
        public string BaseDir { get; set; }
        public string CompareWith { get; set; }
        public int[] imageSize { get; set; } = new int[] { 4656, 3496 };

        public bool edit_autoSave { get; set; }
        public int fontScale { get; set; } = 70;

        public PreprocessConfig PreprocessConfig { get; set; } = new PreprocessConfig();
        public FRTrainCfg FRTrainCfg { get; set; } = new FRTrainCfg();
        public bool ShowListView { get; set; } = false;


        public BaseConfig() { }


        public static BaseConfig GetBaseConfig()
        {
            string root = Directory.GetCurrentDirectory();
            string configPath = Path.Combine(root, "appconfig.json");
            string jsonContent = System.IO.File.ReadAllText(configPath);
            BaseConfig baseConfig = jsonContent.FromJson<BaseConfig>();
            return baseConfig;
        }
        public static void WriteBaseConfig(BaseConfig baseConfig)
        {
            // convert baseConfig to json string
            string configJson = baseConfig.ToJson();
            string rootPath = Directory.GetCurrentDirectory();
            string configFilePath = Path.Combine(rootPath, "appconfig.json");

            // write data to file 
            File.WriteAllText(configFilePath, configJson);
        }
        public static void WriteBaseConfig(string key, object value)
        {
            try
            {
                var baseConfig = GetBaseConfig();
                baseConfig.GetType().GetProperty(key).SetValue(baseConfig, value, null);
                string configJson = baseConfig.ToJson();
                string rootPath = Directory.GetCurrentDirectory();
                string configFilePath = Path.Combine(rootPath, "appconfig.json");

                // write data to file 
                File.WriteAllText(configFilePath, configJson);
            }
            catch (Exception ex)
            {
                Logger.Logger.Error(ex.Message);
            }
        }
    }
    public class PreprocessConfig
    {
        public string Mode { get; set; } = "none";
        public string DefaultModel { get; set; } = "model.onnx";
        public List<int> CropLeft { get; set; }
        public List<int> CropRight { get; set; }
    }

    public class FRTrainCfg
    {
        public string pretrained_model_path_org { get; set; }
        public string model_sample { get; set; }
    }
}
