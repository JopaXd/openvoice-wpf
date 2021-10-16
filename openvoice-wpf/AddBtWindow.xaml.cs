using InTheHand.Net.Sockets;
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
using System.Windows.Shapes;

namespace openvoice_wpf
{
    /// <summary>
    /// Interaction logic for AddBtWindow.xaml
    /// </summary>
    public partial class AddBtWindow : Window
    {

        static BluetoothClient client = new BluetoothClient();
        List<BluetoothDeviceInfo> pd = client.PairedDevices.ToList();

        public AddBtWindow()
        {
            if (pd.Count == 0) {
                MessageBox.Show("Bluetooth not enabled or there are no devices paired with this one!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
            InitializeComponent();
            foreach (BluetoothDeviceInfo device in pd)
            {
                pairedDeviceCBox.Items.Add(device.DeviceName);
            }
            pairedDeviceCBox.SelectedIndex = 0;
        }

        private void createConnBtn_Click(object sender, RoutedEventArgs e)
        {
            string selectedDevice = pairedDeviceCBox.SelectedItem.ToString();
            foreach (BluetoothDeviceInfo device in pd) {
                if (device.DeviceName == selectedDevice) {
                    string type = "bluetooth";
                    string connName = device.DeviceName;
                    string connAddress = device.DeviceAddress.ToString();
                    createNewConnection(type, connName, connAddress);
                    break;
                }
            }
            this.Close();
        }
        private void createNewConnection(string type, string name, string addr)
        {
            string configPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/open_voice/config.json";
            JObject connFile = JObject.Parse(File.ReadAllText(configPath));
            JArray connections = (JArray)connFile["connections"];
            JObject newConn = new JObject();
            newConn.Add(new JProperty("type", type));
            newConn.Add(new JProperty("addr", addr));
            newConn.Add(new JProperty("name", name));
            connections.Add(newConn);
            connFile["connections"] = connections;
            using (StreamWriter f = File.CreateText(configPath))
            using (JsonTextWriter writer = new JsonTextWriter(f))
            {
                connFile.WriteTo(writer);
                writer.Close();
                f.Close();
            }
            return;
        }
    }
}
