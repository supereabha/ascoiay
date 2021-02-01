using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace AOSharp
{
    public class Config
    {
        public ObservableDictionary<string, PluginModel> Plugins { get; set; }

        public ObservableCollection<Profile> Profiles { get; set; }

        protected string _path;

        public static Config Load(string path)
        {
            Config config;

            if (File.Exists(path))
            {
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
            }
            else
            {
                config = new Config()
                {
                    Plugins = new ObservableDictionary<string, PluginModel>(),
                    Profiles = new ObservableCollection<Profile>()
                };
            }

            config._path = path;

            return config;
        }

        public void Save()
        {
            File.WriteAllText(_path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
