using NAudio.CoreAudioApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InTheHand.Net;
using NAudio.Wave;
using FontAwesome.WPF;
using System.Threading;
using System.Text.RegularExpressions;
using InTheHand.Net.Sockets;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;

namespace openvoice_wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string configPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/open_voice/config.json";

        string configDir = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/open_voice/";

        Guid connGuid = new Guid("71019876-227c-4d6f-adea-87d9aa1f7d2c");

        bool status = false;

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

        public void connectBtn_Click(object sender, RoutedEventArgs e) {
            if (status == true) {
                MessageBox.Show("You are already connected to a device!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Grid btnParent = (Grid)((Button)sender).Parent;
            Label addressLbl = FindChild<Label>(btnParent, "connAddr");
            string address = addressLbl.Content.ToString();
            Regex ipv4Regex = new Regex(@"^(?:25[0-5]|2[0-4]\d|[0-1]?\d{1,2})(?:\.(?:25[0-5]|2[0-4]\d|[0-1]?\d{1,2})){3}$");
            Thread connThread;
            if (ipv4Regex.IsMatch(address))
            {
                //This is a wifi connection.
                //A bit odd to determine it, but it works.
                connThread = new Thread(() => wifiConnectionThread(address));
            }
            else {
                //This is a bluetooth connection.
                connThread = new Thread(() => btConnectionThread(BluetoothAddress.Parse(address)));
            }
            connThread.Start();
        }

        public void btConnectionThread(BluetoothAddress address)
        {
            BluetoothClient client = new BluetoothClient();
            try
            {
                client.Connect(address, connGuid);
            }
            catch (Exception e) {
                MessageBox.Show(e.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            //Successfully connected.
            Stream peerStream = client.GetStream();
            peerStream.ReadTimeout = 5000;
            byte[] buf = new byte[4096];
            var bwp = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
            status = true;
            //Determine audio device
            this.Dispatcher.Invoke(() =>
            {
                this.statusLbl.Text = "Status: Connected!";
                this.addrLbl.Text = $"Client Address: {address.ToString()}";
                this.disconnectBtn.Visibility = Visibility.Visible;
            });
            WaveOutEvent wo = null;
            for (int n = -1; n < WaveOut.DeviceCount; n++)
            {
                var caps = WaveOut.GetCapabilities(n);
                if (caps.ProductName == getCurrentAudioDevice()) {
                    wo = new WaveOutEvent { DeviceNumber = n };
                    break;
                }
            }
            wo.Init(bwp);
            wo.Play();
            while (status)
            {
                try
                {
                    int readLen = peerStream.Read(buf, 0, buf.Length);
                    //This checks if the client reads nothing, if it does, it lost the connection.
                    if (readLen == 0)
                    {
                        MessageBox.Show("Lost connection to the server!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    }
                    bwp.AddSamples(buf, 0, readLen);
                }
                //Timeout.
                catch (IOException) {
                    if (status != false)
                    {
                        //Just a check to make sure that this does not pop up even when the user hits the disconnect button.
                        MessageBox.Show("Lost connection to the server!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                }
            }
            //We set status here to false so that if the socket loses the connection, we make sure the status is false.
            status = false;
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.statusLbl.Text = "Status: Not Connected.";
                    this.addrLbl.Text = "Client Address: Not Connected.";
                    this.disconnectBtn.Visibility = Visibility.Hidden;
                });
            }
            catch (TaskCanceledException e) {
                //Do nothing.
                //This error occures when the window closes and the client is still connected.
            }

            client.Close();
        }

        public void wifiConnectionThread(string address)
        {
            UdpClient udp = null;
            try
            {
                udp = new UdpClient(50005);
            }
            catch (SocketException e) {
                //Do nothing.
                //Usually occurs when the user clicks the connect button multiple times.
                return;
                
            }
            udp.Client.ReceiveTimeout = 5000;
            udp.Connect(address, 50005);
            Byte[] sendBytes = Encoding.ASCII.GetBytes(System.Net.Dns.GetHostName());
            udp.Send(sendBytes, sendBytes.Length);
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Parse(address), 0);
            //Connect function does not block until client has connected and tries to read from the socket until it reaches timeout.
            //This tries to read to make sure the client has actually connected before proceding.
            try {
                udp.Receive(ref RemoteIpEndPoint);
            }
            catch (SocketException) {
                MessageBox.Show("Failed to connect to the server!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                udp.Close();
                return;
            }
            var bwp = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
            status = true;
            this.Dispatcher.Invoke(() =>
            {
                this.statusLbl.Text = "Status: Connected!";
                this.addrLbl.Text = $"Client Address: {address}";
                this.disconnectBtn.Visibility = Visibility.Visible;
            });
            WaveOutEvent wo = null;
            for (int n = -1; n < WaveOut.DeviceCount; n++)
            {
                var caps = WaveOut.GetCapabilities(n);
                if (caps.ProductName == getCurrentAudioDevice())
                {
                    wo = new WaveOutEvent { DeviceNumber = n };
                    break;
                }
            }
            wo.Init(bwp);
            wo.Play();
            while (true)
            {
                if (status == false) {
                    //Send the server the "dc" keyword that lets the server know the client has disconnected.
                    Byte[] sendDc = Encoding.ASCII.GetBytes("dc");
                    udp.Send(sendDc, sendDc.Length);
                    break;
                }
                try
                {
                    Byte[] receiveBytes = udp.Receive(ref RemoteIpEndPoint);
                    bwp.AddSamples(receiveBytes, 0, receiveBytes.Length);
                }
                //Timeout
                catch (SocketException)
                {
                    if (status != false) {
                        //Just a check to make sure that this does not pop up even when the user hits the disconnect button.
                        MessageBox.Show("Lost connection to the server!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                }
            }
            //We set status here to false so that if the socket loses the connection, we make sure the status is false.
            status = false;
            try
            {
                this.Dispatcher.Invoke(() =>
                {
                    this.statusLbl.Text = "Status: Not Connected.";
                    this.addrLbl.Text = "Client Address: Not Connected.";
                    this.disconnectBtn.Visibility = Visibility.Hidden;
                });
            }
            catch (TaskCanceledException e) { 
                //Do nothing.
                //This error occures when the window closes and the client is still connected.
            }
            udp.Close();
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
                connectBtn.Click += connectBtn_Click;
                Button forgetBtn = new Button() { Margin = new Thickness(0, 50, 0, 0), Name = "connForgetBtn", HorizontalAlignment = HorizontalAlignment.Right, Width = 120 };
                forgetBtn.SetValue(Grid.ColumnProperty, 2);
                forgetBtn.SetResourceReference(ContentControl.ContentProperty, "MaterialDesignRaisedButton");
                forgetBtn.Content = "Forget";
                forgetBtn.Click += forgetBtn_Click;
                connectionGrid.Children.Add(connectBtn);
                connectionGrid.Children.Add(forgetBtn);
                connectionGrid.Children.Add(connNameLbl);
                connectionGrid.Children.Add(connAddrLbl);
                connectionGrid.Children.Add(connIcn);
                connBorder.Child = connectionGrid;
                connPanel.Children.Add(connBorder);
            }
        }

        public static T FindChild<T>(DependencyObject parent, string childName)
   where T : DependencyObject
        {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                T childType = child as T;
                if (childType == null)
                {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child. 
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    // If the child's name is set for search
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        // if the child's name is of the request name
                        foundChild = (T)child;
                        break;
                    }
                }
                else
                {
                    // child element found.
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }

        private void forgetBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult forgetPrompt = MessageBox.Show("Are you sure you want to forget this connection?", "Forget Connection", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (forgetPrompt == MessageBoxResult.Yes) {
                Grid btnParent = (Grid)((Button)sender).Parent;
                Label addressLbl = FindChild<Label>(btnParent, "connAddr");
                forgetConnection(addressLbl.Content.ToString());
                VisualiseConnections();
            }
        }

        private void forgetConnection(string address) {
            JObject connFile = JObject.Parse(File.ReadAllText(configPath));
            JArray connections = (JArray)connFile["connections"];
            foreach (JObject conn in connections) {
                if ((string)conn["addr"] == address) {
                    connections.Remove(conn);
                    using (StreamWriter f = File.CreateText(configPath))
                    using (JsonTextWriter writer = new JsonTextWriter(f))
                    {
                        connFile.WriteTo(writer);
                        writer.Close();
                        f.Close();
                    }
                    break;
                }
            }
            return;
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

        private void disconnectBtn_Click(object sender, RoutedEventArgs e)
        {
            status = false;
            this.addrLbl.Text = "Client Address: Not Connected.";
            this.statusLbl.Text = "Status: Not Connected.";
            this.disconnectBtn.Visibility = Visibility.Hidden;
        }

        private void mainWindow_Closed(object sender, EventArgs e)
        {
            //Just make sure none of the threads keep running.
            status = false;
        }
    }
}