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
using FontAwesome.WPF;

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
            VisualiseConnections();
        }
        private List<List<string>> getConnections()
        {
            JObject connFile = JObject.Parse(File.ReadAllText(configPath));
            JArray connectionsJson = (JArray)connFile["connections"];
            List<List<string>> connections = new List<List<string>> { };
            foreach (JObject conn in connectionsJson)
            {
                string type = conn["type"].ToString();
                string name = conn["name"].ToString();
                string addr = conn["addr"].ToString();
                List<string> newConn = new List<string> { };
                newConn.Add(type);
                newConn.Add(name);
                newConn.Add(addr);
                connections.Add(newConn);
            }
            return connections;
        }

        public void VisualiseConnections() {
            connPanel.Children.Clear();
            List<List<string>> conns = getConnections();
            foreach (List<string> conn in conns) {
                string type = conn[0];
                string name = conn[1];
                string addr = conn[2];
                //Setting up border.
                Border connBorder = new Border();
                connBorder.BorderThickness = new Thickness(2,2,2,2);
                connBorder.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#666666"));
                connBorder.SetResourceReference(ContentControl.ContentProperty, "MaterialDesignToolBarMainPanelBorderStyle");
                //Setting up the grid.
                Grid connectionGrid = new Grid();
                ColumnDefinition firstCol = new ColumnDefinition() { Width=new GridLength(200) };
                ColumnDefinition secondCol = new ColumnDefinition();
                ColumnDefinition thirdCol = new ColumnDefinition();
                ColumnDefinition fourthCol = new ColumnDefinition() { Width = new GridLength(20) };
                connectionGrid.ColumnDefinitions.Add(firstCol);
                connectionGrid.ColumnDefinitions.Add(secondCol);
                connectionGrid.ColumnDefinitions.Add(thirdCol);
                connectionGrid.ColumnDefinitions.Add(fourthCol);
                RowDefinition firstRow = new RowDefinition();
                connectionGrid.RowDefinitions.Add(firstRow);
                //Creating the icon.
                FontAwesome.WPF.FontAwesome connIcn = new FontAwesome.WPF.FontAwesome();
                connIcn.Foreground = new SolidColorBrush(Colors.White);
                connIcn.Padding = new Thickness(20,20,20,20);
                connIcn.FontSize = 80;
                connIcn.Margin = new Thickness(0, 0, 10, 0);
                connIcn.SetValue(Grid.ColumnProperty, 0);
                if (type == "bluetooth")
                {
                    connIcn.Icon = FontAwesomeIcon.Bluetooth;
                }
                else {
                    //Wifi
                    connIcn.Icon = FontAwesomeIcon.Wifi;
                }
                //Creating the labels.
                Label connNameLbl = new Label() { Name="connName", Content = name, Padding = new Thickness(0,30,0,0),  FontFamily = new FontFamily("Segoe UI Light"), FontSize = 23, Foreground = new SolidColorBrush(Colors.White) };
                connNameLbl.SetValue(Grid.ColumnProperty, 1);
                Label connAddrLbl = new Label() { Name = "connAddr", Content = addr, Padding = new Thickness(0, 60, 0, 0), FontFamily = new FontFamily("Segoe UI Light"), FontSize = 23, Foreground = new SolidColorBrush(Colors.White) };
                connAddrLbl.SetValue(Grid.ColumnProperty, 1);
                //Creating the buttons.
                Button connectBtn = new Button() { Margin = new Thickness(0,-50,0,0), Name="connConnectBtn", HorizontalAlignment=HorizontalAlignment.Right, Width=120};
                connectBtn.SetValue(Grid.ColumnProperty, 2);
                connectBtn.SetResourceReference(ContentControl.ContentProperty, "MaterialDesignRaisedButton");
                connectBtn.Content = "Connect";
                Button forgetBtn = new Button() { Margin = new Thickness(0, 50, 0, 0), Name = "connForgetBtn", HorizontalAlignment = HorizontalAlignment.Right, Width = 120 };
                forgetBtn.SetValue(Grid.ColumnProperty, 2);
                forgetBtn.SetResourceReference(ContentControl.ContentProperty, "MaterialDesignRaisedButton");
                forgetBtn.Content = "Forget";
                connectionGrid.Children.Add(connectBtn);
                connectionGrid.Children.Add(forgetBtn);
                connectionGrid.Children.Add(connNameLbl);
                connectionGrid.Children.Add(connAddrLbl);
                connectionGrid.Children.Add(connIcn);
                connBorder.Child = connectionGrid;
                connPanel.Children.Add(connBorder);
            }
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
            wifiWindow.Closed += onConnectionCreateWindow_Close;
            wifiWindow.Show();
        }

        private void createBtConnBtn_Click(object sender, RoutedEventArgs e)
        {
            AddBtWindow btWindow = new AddBtWindow();
            try
            {
                btWindow.Show();
                btWindow.Closed += onConnectionCreateWindow_Close;
            }
            catch { 
                //Meaning the bluetooth error occured in the AddBtWindow. Do nothing, the user is already informed.
            }
        }

        private void onConnectionCreateWindow_Close(object sender, EventArgs e)
        {
            VisualiseConnections();
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