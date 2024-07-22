using annotation;
using Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FR
{
    internal class FRWkDir
    {
        public class ConfigJson
        {
            public class model
            {
                public string name;
                public string pipeline_config_path;
                public string retrain_path;
            }
            [JsonProperty("list model")]
            public List<model> list_model;
            [JsonProperty("train model")]
            public string train_model;
        }

        public FRWkDir(string model, string baseDir)
        {
            this.model = model;
            this.baseDir = baseDir;
        }
        public string baseDir = "D:\\Sync_Door\\DoorScript";
        private string model;
        public IEnumerable<string> labels;
        public string pretrained_model_path_org;
        public string model_sample;

        public string data => Path.Combine(baseDir, "data_set", $"model_{model}\\data");
        public string annotations => Path.Combine(baseDir, "annotations", $"model_{model}");
        public string config => Path.Combine(baseDir, "config", $"model_{model}");
        public string config_json => Path.Combine(baseDir, "config\\config.json");
        public string models => Path.Combine(baseDir, "models", $"model_{model}");
        public string pretrained_model => Path.Combine(baseDir, "pretrained_model", $"model_{model}");



        public void CreateLabelMap(IEnumerable<string> labels)
        {
            string outDir = annotations;
            if (!Directory.Exists(outDir)) { Directory.CreateDirectory(outDir); }

            var mapFile = Path.Combine(outDir, "label_map.pbtxt");
            var lines = new List<string>();
            var idx = 1;
            foreach (var label in labels)
            {
                lines.Add($"item {{\r\n    id: {idx}\r\n    name: '{label}'\r\n}}");
                idx++;
            }
            File.WriteAllLines(mapFile, lines);
        }
        public bool CreateConfig(string template, int num_classes)
        {
            try
            {
                string outDir = config;
                // pipeline_FasterRetinanet152.config
                if (!Directory.Exists(outDir)) { Directory.CreateDirectory(outDir); }

                var curModel = model;
                var cfgFile = Path.Combine(outDir, "pipeline_FasterRetinanet152.config");
                if (!File.Exists(template)) { throw new Exception("template not exist"); };
                var lines = File.ReadAllLines(template);
                var newData = new List<string>();
                foreach (var line in lines)
                {
                    if (line.Contains("num_classes"))
                    {
                        newData.Add($"    num_classes: {num_classes}");
                    }
                    else if (line.Contains("model_7230B086XA"))
                    {
                        newData.Add(line.Replace("model_7230B086XA", $"model_{curModel}"));
                    }
                    else
                    {
                        newData.Add(line);
                    }
                }
                File.WriteAllLines(cfgFile, newData);
                File.Copy(cfgFile, Path.Combine(outDir, "pipeline_FasterRetinanet152_retrain.config"), true);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Logger.Error(ex.Message);
                return false;
            }
        }
        public bool UpdateCfgJson()
        {
            var txt = File.ReadAllText(config_json);
            var configJson = txt.FromJson<ConfigJson>();
            var found = configJson.list_model.Find(m => m.name == $"model_{model}");
            if (found == null)
            {
                configJson.list_model.Add(new ConfigJson.model
                {
                    name = $"model_{model}",
                    pipeline_config_path = $"config/model_{model}/pipeline_FasterRetinanet152.config",
                    retrain_path = $"config/model_{model}/pipeline_FasterRetinanet152_retrain.config"
                });
                File.WriteAllText(config_json, configJson.ToJson());
                return true;
            }
            return false;
        }

        internal void Init()
        {
            // create label_map
            this.CreateLabelMap(labels);

            // create config
            var template = Path.Combine(this.model_sample, "pipeline_FasterRetinanet152.config");
            this.CreateConfig(template, labels.Count());

            // copy pretained
            var pretrained_model_path_org = this.pretrained_model_path_org;
            if (!Directory.Exists(this.pretrained_model)) { Directory.CreateDirectory(this.pretrained_model); }
            AnntShell.CopyDir(pretrained_model_path_org, this.pretrained_model);

            // clean models\model_xxx
            if (!Directory.Exists(this.models)) { Directory.CreateDirectory(this.models); }
            AnntShell.CleanDir(this.models);

            // update config.json
            this.UpdateCfgJson();
        }
    }
}
