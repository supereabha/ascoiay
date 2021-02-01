using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows.Threading;
using System.Collections.ObjectModel;

namespace AOSharp.Models
{
    public class ProfilesModel
    {
        public ObservableCollection<Profile> Profiles { get; set; }

        private readonly DispatcherTimer _timer;

        public ProfilesModel(Config config)
        {
            Profiles = new ObservableCollection<Profile>(config.Profiles);
            RefreshProfiles(null, null);

            _timer = new DispatcherTimer();
            _timer.Tick += new EventHandler(RefreshProfiles);
            _timer.Interval = new TimeSpan(0, 0, 1);
            _timer.Start();
        }

        private void RefreshProfiles(object sender, EventArgs e)
        {
            foreach (Profile profile in Profiles)
                profile.IsActive = false;

            Process[] aoClients = Process.GetProcessesByName("AnarchyOnline");

            foreach (Process aoClient in aoClients)
            {
                string[] splitTitle = aoClient.MainWindowTitle.Split('-', ' ');

                if (splitTitle.Length != 5)
                    continue;

                Profile profile = Profiles.FirstOrDefault(x => x.Name == splitTitle[4]);

                if(profile == null)
                {
                    profile = new Profile()
                    {
                        Name = splitTitle[4]
                    };

                    Profiles.Add(profile);
                }

                profile.IsActive = true;
                profile.Process = aoClient;
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
