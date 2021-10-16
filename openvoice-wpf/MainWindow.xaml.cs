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
using InTheHand.Net;
using NAudio.Wave;

namespace openvoice_wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string configPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/open_voice/config.json";

        string configDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/open_voice/";

        public MainWindow()
        {
            MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            if (!File.Exists(configPath))
            {
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
            List<string> aDevices = getAudioDevices();
            string defaultAudioDevice = getCurrentAudioDevice();
            int deviceIndex = aDevices.FindIndex(a => a == defaultAudioDevice);
            if (deviceIndex == -1)
            {
                //Meaning the default device was not found, select the system default one.
                deviceIndex = aDevices.FindIndex(a => a == deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console).FriendlyName);
            }
            foreach (string device in aDevices) {
                audioDeviceCBox.Items.Add(device);
            }
            audioDeviceCBox.SelectedIndex = deviceIndex;
        }

        private List<string> getAudioDevices() {
            List<string> audioDevices = new List<string> { };
            for (int n = -1; n < WaveOut.DeviceCount; n++)
            {
                var caps = WaveOut.GetCapabilities(n);
                audioDevices.Add(caps.ProductName);
            }
            return audioDevices;
        }

        private string getCurrentAudioDevice()
        {
            JObject connFile = JObject.Parse(File.ReadAllText(configPath));
            string currentAudioDevice = (string)connFile["audioDevice"];
            return currentAudioDevice;
        }

        private void createWifiConnBtn_Click(object sender, RoutedEventArgs e)
        {
            AddWifiWindow wifiWindow = new AddWifiWindow();
            wifiWindow.Show();
        }

        private void createBtConnBtn_Click(object sender, RoutedEventArgs e)
        {
            AddBtWindow btWindow = new AddBtWindow();
            try
            {
                btWindow.Show();
            }
            catch { 
                //Meaning the bluetooth error occured in the AddBtWindow. Do nothing, the user is already informed.
            }
        }

        private void changeAudioDevice(string name)
        {
            JObject connFile = JObject.Parse(File.ReadAllText(configPath));
            connFile["audioDevice"] = name;
            using (StreamWriter f = File.CreateText(configPath))
            using (JsonTextWriter writer = new JsonTextWriter(f))
            {
                connFile.WriteTo(writer);
                writer.Close();
                f.Close();
            }
        }

        private void audioDevice_onChange(object sender, SelectionChangedEventArgs e)
        {
            changeAudioDevice(audioDeviceCBox.SelectedItem.ToString());
        }
    }
}