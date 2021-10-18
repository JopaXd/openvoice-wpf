using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;

namespace openvoice_wpf
{
    /// <summary>
    /// Interaction logic for AddWifiWindow.xaml
    /// </summary>
    public partial class AddWifiWindow : Window
    {
        public AddWifiWindow()
        {
            InitializeComponent();
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

        private void createConnBtn_Click(object sender, RoutedEventArgs e)
        {
            string ipAddress = this.addressTbox.Text;
            string connName = this.nameTbox.Text;
            string connType = "wifi";
            if (ipAddress != "" && connName != "")
            {
                Regex ipv4Regex = new Regex(@"^(?:25[0-5]|2[0-4]\d|[0-1]?\d{1,2})(?:\.(?:25[0-5]|2[0-4]\d|[0-1]?\d{1,2})){3}$");
                if (ipv4Regex.IsMatch(ipAddress))
                {
                    createNewConnection(connType, connName, ipAddress);
                    this.Close();
                }
                else {
                    MessageBox.Show("You must provide a valid IPv4 address!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else {
                MessageBox.Show("You must provide both the name and the ip address!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
