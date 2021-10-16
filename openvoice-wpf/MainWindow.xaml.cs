using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace openvoice_wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            string configPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/open_voice/config.json";

            string configDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/open_voice/";

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            if (!File.Exists(configPath))
            {
                MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
                JObject data = new JObject();
                JProperty conns = new JProperty("connections", new JArray());
                JProperty audioDevice = new JProperty("audioDevice", deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console).FriendlyName);
                data.Add(conns);
                data.Add(audioDevice);
                using (StreamWriter f = File.CreateText(configPath))
                using (JsonTextWriter writer = new JsonTextWriter(f))
                {
                    data.WriteTo(writer);
                    writer.Close();
                    f.Close();
                }
            }
            InitializeComponent();
        }

        private void createWifiConnBtn_Click(object sender, RoutedEventArgs e)
        {
            AddWifiWindow wifiWindow = new AddWifiWindow();
            wifiWindow.Show();
        }
    }
}