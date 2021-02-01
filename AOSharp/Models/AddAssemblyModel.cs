using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;

namespace AOSharp.Models
{
    public class AddAssemblyModel : INotifyPropertyChanged
    {
        public string _dllPath;

        public string DllPath
        {
            get { return _dllPath; }
            set
            {
                _dllPath = value;
                OnPropertyChanged("DllPath");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
