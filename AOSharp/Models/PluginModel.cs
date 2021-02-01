using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Windows.Input;
using AOSharp.Core;

namespace AOSharp
{
    public class PluginModel : INotifyPropertyChanged
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public string Path { get; set; }

        [JsonIgnore]
        public bool _isEnabled;

        [JsonIgnore]
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set
            {
                _isEnabled = value;
                OnPropertyChanged("IsEnabled");
            }
        }

        [JsonIgnore]
        public ICommand RemoveCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public PluginModel()
        {
            this.RemoveCommand = new SimpleCommand() { ExecuteDelegate = Remove };
        }

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void Remove(object obj)
        {
            var args = (Tuple<ObservableDictionary<string, PluginModel>, string>)obj;
            args.Item1.Remove(args.Item2, this);
        }
    }
}
