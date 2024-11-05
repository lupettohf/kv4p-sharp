using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.CoreAudioApi; // For volume control and level meters
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace RadioControllerApp
{
    public class FormRadio : Form
    {
        // Pre-buffering fields
        private const int PreBufferSize = 5000; // Adjust as needed
        private ConcurrentQueue<byte[]> preBufferQueue = new ConcurrentQueue<byte[]>();
        private int preBufferedBytes = 0;
        private bool preBufferingComplete = false;

        private RadioController radioController;
        private WaveInEvent waveIn;
        private BufferedWaveProvider receivedAudioBuffer;
        private WaveOutEvent waveOut;

        private bool isRecording = false;
        private bool isTransmitting = false;

        // PTT Key Handling
        private Keys pttKey = Keys.Space;
        private bool waitingForPTTKey = false;

        // ESP32 Vendor IDs and Product IDs
        private readonly List<int> ESP32_VENDOR_IDS = new List<int> { 4292 };
        private readonly List<int> ESP32_PRODUCT_IDS = new List<int> { 60000 };

        // UI Controls
        private ComboBox cmbPortName;
        private Button btnOpenConnection;
        private Button btnCloseConnection;

        private TextBox txtTXFrequency;
        private TextBox txtRXFrequency;
        private ComboBox cmbTone;
        private ComboBox cmbSquelchLevel;
        private Button btnTune;

        private CheckBox chkEmphasis;
        private CheckBox chkHighpass;
        private CheckBox chkLowpass;
        private Button btnSetFilters;

        private Button btnSetPTTKey;

        private TextBox txtStatus;

        private Panel pnlStatusIndicator;

        private ComboBox cmbRecordingDevice;
        private ComboBox cmbPlaybackDevice;

        private TrackBar trkRecordingVolume;
        private TrackBar trkPlaybackVolume;
        private Label lblRecordingVolume;
        private Label lblPlaybackVolume;

        // Tone mappings
        private Dictionary<string, int> toneMappings = new Dictionary<string, int>
{
    { "None", 0 },
    { "67.0", 1 },
    { "71.9", 2 },
    { "74.4", 3 },
    { "77.0", 4 },
    { "79.7", 5 },
    { "82.5", 6 },
    { "85.4", 7 },
    { "88.5", 8 },
    { "91.5", 9 },
    { "94.8", 10 },
    { "97.4", 11 },
    { "100.0", 12 },
    { "103.5", 13 },
    { "107.2", 14 },
    { "110.9", 15 },
    { "114.8", 16 },
    { "118.8", 17 },
    { "123.0", 18 },
    { "127.3", 19 },
    { "131.8", 20 },
    { "136.5", 21 },
    { "141.3", 22 },
    { "146.2", 23 },
    { "151.4", 24 },
    { "156.7", 25 },
    { "162.2", 26 },
    { "167.9", 27 },
    { "173.8", 28 },
    { "179.9", 29 },
    { "186.2", 30 },
    { "192.8", 31 },
    { "203.5", 32 },
    { "210.7", 33 },
    { "218.1", 34 },
    { "225.7", 35 },
    { "233.6", 36 },
    { "241.8", 37 },
    { "250.3", 38 }
};


        public FormRadio()
        {
            InitializeComponent();
            InitializeRadioController();
            PopulateSerialPorts();
            PopulateAudioDevices();
            InitializeWaveOut();
            LoadConfigurations();
        }

        private void InitializeComponent()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi; // Enable DPI scaling

            // Form properties
            this.Text = "Radio Controller";
            this.Size = new System.Drawing.Size(800, 700);
            this.MinimumSize = new System.Drawing.Size(600, 600);
            this.FormClosing += FormRadio_FormClosing;

            // Enable KeyPreview to capture key events
            this.KeyPreview = true;
            this.KeyDown += FormRadio_KeyDown;
            this.KeyUp += FormRadio_KeyUp;

            // Initialize controls
            InitializeControls();
        }

        private void InitializeControls()
        {
            // Main TableLayoutPanel
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                AutoSize = true,
            };
            this.Controls.Add(mainLayout);

            // Insert row styles
            mainLayout.RowStyles.Insert(0, new RowStyle(SizeType.Absolute, 20F)); // Status Indicator
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Connection Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Frequency Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Filter Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Audio Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Status TextBox

            // Status Indicator Panel
            pnlStatusIndicator = new Panel
            {
                Height = 20,
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.Green // Assume green for RX
            };
            mainLayout.Controls.Add(pnlStatusIndicator, 0, 0);

            // Connection Controls GroupBox
            GroupBox grpConnection = CreateConnectionControls();
            grpConnection.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(grpConnection, 0, 1);

            // Frequency Controls GroupBox
            GroupBox grpFrequency = CreateFrequencyControls();
            grpFrequency.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(grpFrequency, 0, 2);

            // Filter Controls GroupBox
            GroupBox grpFilters = CreateFilterControls();
            grpFilters.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(grpFilters, 0, 3);

            // Audio Controls GroupBox
            GroupBox grpAudio = CreateAudioControls();
            grpAudio.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(grpAudio, 0, 4);

            // Status TextBox
            txtStatus = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical
            };
            mainLayout.Controls.Add(txtStatus, 0, 5);

            // Add padding to each group box
            foreach (Control control in mainLayout.Controls)
            {
                if (control is GroupBox)
                {
                    control.Padding = new Padding(10, 10, 10, 10);
                    control.Margin = new Padding(0, 0, 0, 10); // Add bottom margin
                }
            }

        }

        private GroupBox CreateConnectionControls()
        {
            GroupBox grpConnection = new GroupBox
            {
                Text = "Connection Controls",
                AutoSize = true,
                Dock = DockStyle.Fill
            };

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                AutoSize = true
            };
            grpConnection.Controls.Add(layout);

            Label lblPortName = new Label { Text = "Port Name:", Anchor = AnchorStyles.Right };
            cmbPortName = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left, Width = 200 };
            btnOpenConnection = new Button { Text = "Open Connection", AutoSize = true };
            btnCloseConnection = new Button { Text = "Close Connection", AutoSize = true };

            btnOpenConnection.Click += btnOpenConnection_Click;
            btnCloseConnection.Click += btnCloseConnection_Click;

            layout.Controls.Add(lblPortName, 0, 0);
            layout.Controls.Add(cmbPortName, 1, 0);
            layout.Controls.Add(btnOpenConnection, 2, 0);
            layout.Controls.Add(btnCloseConnection, 3, 0);

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // ComboBox
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Button
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Button

            return grpConnection;
        }

        private GroupBox CreateFrequencyControls()
        {
            GroupBox grpFrequency = new GroupBox
            {
                Text = "Frequency Controls",
                AutoSize = true,
                Dock = DockStyle.Fill
            };

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                AutoSize = true
            };
            grpFrequency.Controls.Add(layout);

            Label lblTXFrequency = new Label { Text = "TX Frequency:", Anchor = AnchorStyles.Right };
            txtTXFrequency = new TextBox { Text = "146.520", Anchor = AnchorStyles.Left };
            Label lblRXFrequency = new Label { Text = "RX Frequency:", Anchor = AnchorStyles.Right };
            txtRXFrequency = new TextBox { Text = "146.520", Anchor = AnchorStyles.Left };
            Label lblTone = new Label { Text = "Tone:", Anchor = AnchorStyles.Right };
            cmbTone = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left };
            Label lblSquelchLevel = new Label { Text = "Squelch Level:", Anchor = AnchorStyles.Right };
            cmbSquelchLevel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left };
            btnTune = new Button { Text = "Tune to Frequency", AutoSize = true };

            btnTune.Click += btnTune_Click;

            layout.Controls.Add(lblTXFrequency, 0, 0);
            layout.Controls.Add(txtTXFrequency, 1, 0);
            layout.Controls.Add(lblRXFrequency, 2, 0);
            layout.Controls.Add(txtRXFrequency, 3, 0);
            layout.Controls.Add(lblTone, 4, 0);
            layout.Controls.Add(cmbTone, 5, 0);

            layout.Controls.Add(lblSquelchLevel, 0, 1);
            layout.Controls.Add(cmbSquelchLevel, 1, 1);
            layout.SetColumnSpan(btnTune, 4);
            layout.Controls.Add(btnTune, 2, 1);
            // Populate cmbTone
            foreach (var tone in toneMappings.Keys)
            {
                cmbTone.Items.Add(tone);
            }
            cmbTone.SelectedIndex = 0; // Default to "None"
            for (int i = 0; i <= 9; i++)
            {
                cmbSquelchLevel.Items.Add(i.ToString());
            }
            cmbSquelchLevel.SelectedIndex = 1; // Default to 1
            // Adjust column styles
            for (int i = 0; i < 6; i += 2)
            {
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.66F)); // TextBox
            }

            return grpFrequency;
        }

        private GroupBox CreateFilterControls()
        {
            GroupBox grpFilters = new GroupBox
            {
                Text = "Filter Controls",
                AutoSize = true,
                Dock = DockStyle.Fill
            };

            FlowLayoutPanel layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
            };
            grpFilters.Controls.Add(layout);

            chkEmphasis = new CheckBox { Text = "Emphasis", AutoSize = true };
            chkHighpass = new CheckBox { Text = "Highpass", AutoSize = true };
            chkLowpass = new CheckBox { Text = "Lowpass", AutoSize = true };
            btnSetFilters = new Button { Text = "Set Filters", AutoSize = true };

            btnSetFilters.Click += btnSetFilters_Click;

            layout.Controls.Add(chkEmphasis);
            layout.Controls.Add(chkHighpass);
            layout.Controls.Add(chkLowpass);
            layout.Controls.Add(btnSetFilters);

            return grpFilters;
        }

        private GroupBox CreateAudioControls()
        {
            GroupBox grpAudio = new GroupBox
            {
                Text = "Audio Controls",
                AutoSize = true,
                Dock = DockStyle.Fill
            };

            // Adjusted layout to place recording and playback controls side by side
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                AutoSize = true
            };
            grpAudio.Controls.Add(layout);

            Label lblRecordingDevice = new Label { Text = "Recording Device:", Anchor = AnchorStyles.Right };
            cmbRecordingDevice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left, Width = 200 };
            Label lblPlaybackDevice = new Label { Text = "Playback Device:", Anchor = AnchorStyles.Right };
            cmbPlaybackDevice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left, Width = 200 };

            lblRecordingVolume = new Label { Text = "Recording Volume:", Anchor = AnchorStyles.Right };
            trkRecordingVolume = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, TickFrequency = 10, Anchor = AnchorStyles.Left, Width = 150 };
            lblPlaybackVolume = new Label { Text = "Playback Volume:", Anchor = AnchorStyles.Right };
            trkPlaybackVolume = new TrackBar { Minimum = 0, Maximum = 100, Value = 100, TickFrequency = 10, Anchor = AnchorStyles.Left, Width = 150 };

            btnSetPTTKey = new Button { Text = "Set PTT Key", AutoSize = true };
            btnSetPTTKey.Click += btnSetPTTKey_Click;

            // First row: Recording and Playback Device labels and comboboxes
            layout.Controls.Add(lblRecordingDevice, 0, 0);
            layout.Controls.Add(cmbRecordingDevice, 1, 0);
            layout.Controls.Add(lblPlaybackDevice, 2, 0);
            layout.Controls.Add(cmbPlaybackDevice, 3, 0);

            // Second row: Recording and Playback Volume labels and trackbars
            layout.Controls.Add(lblRecordingVolume, 0, 1);
            layout.Controls.Add(trkRecordingVolume, 1, 1);
            layout.Controls.Add(lblPlaybackVolume, 2, 1);
            layout.Controls.Add(trkPlaybackVolume, 3, 1);

            // Third row: PTT Key button
            layout.Controls.Add(btnSetPTTKey, 0, 2);
            layout.SetColumnSpan(btnSetPTTKey, 4);

            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            cmbPlaybackDevice.SelectedIndexChanged += CmbPlaybackDevice_SelectedIndexChanged;
            cmbRecordingDevice.SelectedIndexChanged += CmbRecordingDevice_SelectedIndexChanged;

            trkPlaybackVolume.ValueChanged += TrkPlaybackVolume_ValueChanged;
            trkRecordingVolume.ValueChanged += TrkRecordingVolume_ValueChanged;

            return grpAudio;
        }

        private void InitializeRadioController()
        {
            // Disable controls that require an open connection
            ToggleControls(false);
        }

        private void PopulateSerialPorts()
        {
            cmbPortName.Items.Clear();

            // Get all available serial ports
            var portNames = SerialPort.GetPortNames();

            // Use ManagementObjectSearcher to get more info
            var searcher = new ManagementObjectSearcher("SELECT * FROM WIN32_SerialPort");
            var ports = searcher.Get();

            List<SerialPortInfo> portList = new List<SerialPortInfo>();

            foreach (var port in ports)
            {
                string name = port["Name"].ToString();
                string deviceId = port["DeviceID"].ToString();
                string pnpDeviceId = port["PNPDeviceID"].ToString();

                // Extract Vendor ID and Product ID from PNPDeviceID
                // Example PNPDeviceID: USB\VID_10C4&PID_EA60\0001
                string vid = GetPropertyFromDeviceID(pnpDeviceId, "VID");
                string pid = GetPropertyFromDeviceID(pnpDeviceId, "PID");

                int vendorId = 0;
                int productId = 0;

                try
                {
                    vendorId = Convert.ToInt32(vid, 16);
                    productId = Convert.ToInt32(pid, 16);
                }
                catch
                {
                    // Ignore if unable to parse VID and PID
                }

                bool isEsp32 = ESP32_VENDOR_IDS.Contains(vendorId) && ESP32_PRODUCT_IDS.Contains(productId);

                portList.Add(new SerialPortInfo
                {
                    Name = name,
                    DeviceId = deviceId,
                    PnpDeviceId = pnpDeviceId,
                    IsEsp32 = isEsp32,
                });
            }

            // Now populate cmbPortName
            foreach (var portInfo in portList)
            {
                cmbPortName.Items.Add(portInfo);
            }

            // Select the first port
            if (cmbPortName.Items.Count > 0)
                cmbPortName.SelectedIndex = 0;
        }

        private void PopulateAudioDevices()
        {
            // Populate recording devices
            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var capabilities = WaveIn.GetCapabilities(n);
                cmbRecordingDevice.Items.Add(new WaveInDevice { DeviceNumber = n, ProductName = capabilities.ProductName });
            }

            // Select default recording device
            if (cmbRecordingDevice.Items.Count > 0)
                cmbRecordingDevice.SelectedIndex = 0;

            // Populate playback devices
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var device in devices)
            {
                cmbPlaybackDevice.Items.Add(new WaveOutDevice { DeviceNumber = 0, ProductName = device.FriendlyName, MMDevice = device });
            }

            // Select default playback device
            if (cmbPlaybackDevice.Items.Count > 0)
                cmbPlaybackDevice.SelectedIndex = 0;
        }

        private void InitializeWaveOut()
        {
            receivedAudioBuffer = new BufferedWaveProvider(new WaveFormat(44100, 8, 1))
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            waveOut = new WaveOutEvent();

            var selectedPlaybackDevice = cmbPlaybackDevice.SelectedItem as WaveOutDevice;
            if (selectedPlaybackDevice != null)
            {
                //waveOut.DeviceNumber = selectedPlaybackDevice.DeviceNumber;
                // Use NAudio.CoreAudioApi to control playback volume
                //waveOut.DeviceNumber = -1; // Use default device
            }

            waveOut.Init(receivedAudioBuffer);
        }

        private void CmbPlaybackDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Re-initialize waveOut with the selected device
            if (waveOut != null)
            {
                waveOut.Stop();
                waveOut.Dispose();
            }
            InitializeWaveOut();
        }

        private void CmbRecordingDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Do nothing for now. Recording device is set when starting transmission.
        }

        private void TrkPlaybackVolume_ValueChanged(object sender, EventArgs e)
        {
            SetPlaybackVolume(trkPlaybackVolume.Value);
        }

        private void TrkRecordingVolume_ValueChanged(object sender, EventArgs e)
        {
            SetRecordingVolume(trkRecordingVolume.Value);
        }

        private void SetPlaybackVolume(int volume)
        {
            var selectedPlaybackDevice = cmbPlaybackDevice.SelectedItem as WaveOutDevice;
            if (selectedPlaybackDevice != null && selectedPlaybackDevice.MMDevice != null)
            {
                selectedPlaybackDevice.MMDevice.AudioEndpointVolume.MasterVolumeLevelScalar = volume / 100.0f;
            }
        }

        private void SetRecordingVolume(int volume)
        {
            var selectedRecordingDevice = cmbRecordingDevice.SelectedItem as WaveInDevice;
            if (selectedRecordingDevice != null)
            {
                // Setting recording volume is not straightforward with WaveInEvent
                // Need to use NAudio.CoreAudioApi
                var enumerator = new MMDeviceEnumerator();
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                foreach (var device in devices)
                {
                    if (device.FriendlyName == selectedRecordingDevice.ProductName)
                    {
                        device.AudioEndpointVolume.MasterVolumeLevelScalar = volume / 100.0f;
                        break;
                    }
                }
            }
        }

        private void btnOpenConnection_Click(object sender, EventArgs e)
        {
            try
            {
                var selectedPortInfo = cmbPortName.SelectedItem as SerialPortInfo;
                if (selectedPortInfo != null)
                {
                    string portName = selectedPortInfo.DeviceId;
                    radioController = new RadioController(portName);
                    radioController.ErrorOccurred += RadioController_ErrorOccurred;
                    radioController.AudioDataReceived += RadioController_AudioDataReceived;
                    radioController.OpenConnection();

                    radioController.Initialize();
                    btnTune_Click(null, null);
                    // Start RX mode
                    isTransmitting = false;
                    UpdateStatusIndicator(false);

                    AppendStatus($"Connection opened on {portName}.");
                    radioController.StartRXMode();
                    ToggleControls(true);
                }
                else
                {
                    AppendStatus("Please select a valid port.");
                    return;
                }
            }
            catch (Exception ex)
            {
                AppendStatus($"Error opening connection: {ex.Message}");
            }
        }

        private void btnCloseConnection_Click(object sender, EventArgs e)
        {
            try
            {
                if (radioController != null)
                {
                    radioController.CloseConnection();
                    radioController.Dispose();
                    radioController = null;
                    AppendStatus("Connection closed.");
                    ToggleControls(false);
                }
            }
            catch (Exception ex)
            {
                AppendStatus($"Error closing connection: {ex.Message}");
            }
        }

        private void btnTune_Click(object sender, EventArgs e)
        {
            try
            {
                string txFreq = txtTXFrequency.Text.Trim();
                string rxFreq = txtRXFrequency.Text.Trim();

                // Get selected tone
                string selectedTone = cmbTone.SelectedItem.ToString();
                int toneValue = toneMappings[selectedTone];

                // Get selected squelch level
                int squelch = int.Parse(cmbSquelchLevel.SelectedItem.ToString());

                radioController.TuneToFrequency(txFreq, rxFreq, toneValue, squelch);
                AppendStatus($"Tuned to TX: {txFreq}, RX: {rxFreq}, Tone: {selectedTone}, Squelch Level: {squelch}");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error tuning frequency: {ex.Message}");
            }
        }


        private void btnSetFilters_Click(object sender, EventArgs e)
        {
            try
            {
                bool emphasis = chkEmphasis.Checked;
                bool highpass = chkHighpass.Checked;
                bool lowpass = chkLowpass.Checked;

                radioController.SetFilters(emphasis, highpass, lowpass);
                AppendStatus("Filters set.");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error setting filters: {ex.Message}");
            }
        }

        private void btnSetPTTKey_Click(object sender, EventArgs e)
        {
            AppendStatus("Press any key to set as PTT.");
            waitingForPTTKey = true;
        }

        private void FormRadio_KeyDown(object sender, KeyEventArgs e)
        {
            if (waitingForPTTKey)
            {
                pttKey = e.KeyCode;
                waitingForPTTKey = false;
                AppendStatus($"PTT key set to: {pttKey}");
                e.Handled = true;
            }
            else if (e.KeyCode == pttKey)
            {
                if (!isTransmitting)
                {
                    StartTransmission();
                }
                e.Handled = true;
            }
        }

        private void FormRadio_KeyUp(object sender, KeyEventArgs e)
        {
            if (!waitingForPTTKey && e.KeyCode == pttKey)
            {
                if (isTransmitting)
                {
                    StopTransmission();
                }
                e.Handled = true;
            }
        }

        private void StartTransmission()
        {
            try
            {
                if (radioController == null)
                {
                    AppendStatus("Please open the connection first.");
                    return;
                }

                radioController.StartTXMode();
                isTransmitting = true;

                waveIn = new WaveInEvent();

                var selectedRecordingDevice = cmbRecordingDevice.SelectedItem as WaveInDevice;
                if (selectedRecordingDevice != null)
                {
                    waveIn.DeviceNumber = selectedRecordingDevice.DeviceNumber;
                }

                waveIn.WaveFormat = new WaveFormat(44100, 8, 1);
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.StartRecording();

                AppendStatus("Transmission started.");

                // Disable recording device selection during transmission
                cmbRecordingDevice.Enabled = false;

                // Update status indicator
                UpdateStatusIndicator(true);
            }
            catch (Exception ex)
            {
                AppendStatus($"Error starting transmission: {ex.Message}");
            }
        }

        private void StopTransmission()
        {
            try
            {
                if (isTransmitting && waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                    waveIn = null;
                    isTransmitting = false;

                    radioController.EndTXMode();
                    AppendStatus("Transmission stopped.");
                    radioController.StartRXMode();

                    // Enable recording device selection
                    cmbRecordingDevice.Enabled = true;

                    // Update status indicator
                    UpdateStatusIndicator(false);
                }
            }
            catch (Exception ex)
            {
                AppendStatus($"Error stopping transmission: {ex.Message}");
            }
        }

        private async void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                if (isTransmitting && radioController != null)
                {
                    byte[] buffer = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, buffer, e.BytesRecorded);

                    // Send audio data asynchronously
                    await radioController.SendAudioDataAsync(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                AppendStatus($"Error sending audio data: {ex.Message}");
            }
        }

        private void RadioController_ErrorOccurred(object sender, ErrorEventArgs e)
        {
            AppendStatus($"Error: {e.GetException().Message}");
        }

        private void RadioController_AudioDataReceived(object sender, byte[] data)
        {
            Console.WriteLine($"AudioDataReceived called with {data.Length} bytes");

            if (!preBufferingComplete)
            {
                preBufferQueue.Enqueue(data);
                preBufferedBytes += data.Length;

                if (preBufferedBytes >= PreBufferSize)
                {
                    // Pre-buffering complete, start playback
                    preBufferingComplete = true;

                    // Add all pre-buffered data to the buffer
                    while (preBufferQueue.TryDequeue(out byte[] preBufferData))
                    {
                        receivedAudioBuffer.AddSamples(preBufferData, 0, preBufferData.Length);
                    }

                    // Start playback
                    waveOut.Play();
                }
            }
            else
            {
                // Add received audio data to the buffer
                receivedAudioBuffer.AddSamples(data, 0, data.Length);

                // Ensure playback is ongoing
                if (waveOut.PlaybackState != PlaybackState.Playing)
                {
                    waveOut.Play();
                }
            }
        }

        private void AppendStatus(string message)
        {
            if (txtStatus.InvokeRequired)
            {
                txtStatus.Invoke(new Action(() => AppendStatus(message)));
            }
            else
            {
                txtStatus.AppendText($"{DateTime.Now}: {message}{Environment.NewLine}");
            }
        }

        private void UpdateStatusIndicator(bool isTransmitting)
        {
            if (pnlStatusIndicator.InvokeRequired)
            {
                pnlStatusIndicator.Invoke(new Action(() => UpdateStatusIndicator(isTransmitting)));
            }
            else
            {
                if (isTransmitting)
                {
                    pnlStatusIndicator.BackColor = System.Drawing.Color.Red;
                }
                else
                {
                    pnlStatusIndicator.BackColor = System.Drawing.Color.Green;
                }
            }
        }

        private void ToggleControls(bool isEnabled)
        {
            btnTune.Enabled = isEnabled;
            btnSetFilters.Enabled = isEnabled;
            btnSetPTTKey.Enabled = isEnabled;

            // Disable connection buttons appropriately
            btnOpenConnection.Enabled = !isEnabled;
            btnCloseConnection.Enabled = isEnabled;
        }

        private void FormRadio_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Clean up resources
            if (radioController != null)
            {
                radioController.Dispose();
                radioController = null;
            }

            if (waveIn != null)
            {
                waveIn.Dispose();
                waveIn = null;
            }

            if (waveOut != null)
            {
                waveOut.Dispose();
                waveOut = null;
            }

            // Save configurations
            SaveConfigurations();
        }

        private void LoadConfigurations()
        {
            string configFile = "config.ini";
            if (File.Exists(configFile))
            {
                string[] lines = File.ReadAllLines(configFile);
                foreach (string line in lines)
                {
                    if (line.StartsWith("PortName="))
                    {
                        string portName = line.Substring("PortName=".Length);
                        foreach (SerialPortInfo portInfo in cmbPortName.Items)
                        {
                            if (portInfo.DeviceId == portName)
                            {
                                cmbPortName.SelectedItem = portInfo;
                                break;
                            }
                        }
                    }
                    else if (line.StartsWith("PTTKey="))
                    {
                        if (Enum.TryParse(line.Substring("PTTKey=".Length), out Keys key))
                        {
                            pttKey = key;
                        }
                    }
                    else if (line.StartsWith("TXFrequency="))
                    {
                        txtTXFrequency.Text = line.Substring("TXFrequency=".Length);
                    }
                    else if (line.StartsWith("RXFrequency="))
                    {
                        txtRXFrequency.Text = line.Substring("RXFrequency=".Length);
                    }
                    else if (line.StartsWith("Tone="))
                    {
                        if (int.TryParse(line.Substring("Tone=".Length), out int toneValue))
                        {
                            foreach (var item in toneMappings)
                            {
                                if (item.Value == toneValue)
                                {
                                    cmbTone.SelectedItem = item.Key;
                                    break;
                                }
                            }
                        }
                    }
                    else if (line.StartsWith("SquelchLevel="))
                    {
                        cmbSquelchLevel.SelectedItem = line.Substring("SquelchLevel=".Length);
                    }
                    else if (line.StartsWith("Emphasis="))
                    {
                        chkEmphasis.Checked = line.Substring("Emphasis=".Length) == "True";
                    }
                    else if (line.StartsWith("Highpass="))
                    {
                        chkHighpass.Checked = line.Substring("Highpass=".Length) == "True";
                    }
                    else if (line.StartsWith("Lowpass="))
                    {
                        chkLowpass.Checked = line.Substring("Lowpass=".Length) == "True";
                    }
                    else if (line.StartsWith("RecordingDevice="))
                    {
                        if (int.TryParse(line.Substring("RecordingDevice=".Length), out int deviceNumber))
                        {
                            foreach (WaveInDevice device in cmbRecordingDevice.Items)
                            {
                                if (device.DeviceNumber == deviceNumber)
                                {
                                    cmbRecordingDevice.SelectedItem = device;
                                    break;
                                }
                            }
                        }
                    }
                    else if (line.StartsWith("PlaybackDevice="))
                    {
                        string playbackDeviceName = line.Substring("PlaybackDevice=".Length);
                        foreach (WaveOutDevice device in cmbPlaybackDevice.Items)
                        {
                            if (device.ProductName == playbackDeviceName)
                            {
                                cmbPlaybackDevice.SelectedItem = device;
                                break;
                            }
                        }
                    }
                    else if (line.StartsWith("RecordingVolume="))
                    {
                        if (int.TryParse(line.Substring("RecordingVolume=".Length), out int volume))
                        {
                            trkRecordingVolume.Value = volume;
                            SetRecordingVolume(volume);
                        }
                    }
                    else if (line.StartsWith("PlaybackVolume="))
                    {
                        if (int.TryParse(line.Substring("PlaybackVolume=".Length), out int volume))
                        {
                            trkPlaybackVolume.Value = volume;
                            SetPlaybackVolume(volume);
                        }
                    }
                }
            }
        }

        private void SaveConfigurations()
        {
            string configFile = "config.ini";
            string selectedTone = cmbTone.SelectedItem.ToString();
            int toneValue = toneMappings[selectedTone];
            using (StreamWriter writer = new StreamWriter(configFile))
            {
                // Save selected port
                var selectedPortInfo = cmbPortName.SelectedItem as SerialPortInfo;
                if (selectedPortInfo != null)
                {
                    writer.WriteLine($"PortName={selectedPortInfo.DeviceId}");
                }
                else
                {
                    writer.WriteLine("PortName=");
                }

                writer.WriteLine($"PTTKey={pttKey}");
                writer.WriteLine($"TXFrequency={txtTXFrequency.Text}");
                writer.WriteLine($"RXFrequency={txtRXFrequency.Text}");
                writer.WriteLine($"Tone={toneValue}");
                writer.WriteLine($"SquelchLevel={cmbSquelchLevel.SelectedItem}");
                writer.WriteLine($"Emphasis={chkEmphasis.Checked}");
                writer.WriteLine($"Highpass={chkHighpass.Checked}");
                writer.WriteLine($"Lowpass={chkLowpass.Checked}");

                // Save selected recording device
                var selectedRecordingDevice = cmbRecordingDevice.SelectedItem as WaveInDevice;
                if (selectedRecordingDevice != null)
                {
                    writer.WriteLine($"RecordingDevice={selectedRecordingDevice.DeviceNumber}");
                }
                else
                {
                    writer.WriteLine("RecordingDevice=");
                }

                // Save selected playback device
                var selectedPlaybackDevice = cmbPlaybackDevice.SelectedItem as WaveOutDevice;
                if (selectedPlaybackDevice != null)
                {
                    writer.WriteLine($"PlaybackDevice={selectedPlaybackDevice.ProductName}");
                }
                else
                {
                    writer.WriteLine("PlaybackDevice=");
                }

                writer.WriteLine($"RecordingVolume={trkRecordingVolume.Value}");
                writer.WriteLine($"PlaybackVolume={trkPlaybackVolume.Value}");
            }
        }

        private string GetPropertyFromDeviceID(string deviceId, string property)
        {
            string result = "";
            string pattern = property + "_([0-9A-F]{4})";
            var match = Regex.Match(deviceId, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                result = match.Groups[1].Value;
            }
            return result;
        }
    }


    class SerialPortInfo
    {
        public string Name { get; set; }
        public string DeviceId { get; set; }
        public string PnpDeviceId { get; set; }
        public bool IsEsp32 { get; set; }

        public override string ToString()
        {
            if (IsEsp32)
                return Name + " (ESP32)";
            else
                return Name;
        }
    }

    class WaveInDevice
    {
        public int DeviceNumber { get; set; }
        public string ProductName { get; set; }
        public override string ToString()
        {
            return ProductName;
        }
    }

    class WaveOutDevice
    {
        public int DeviceNumber { get; set; }
        public string ProductName { get; set; }
        public MMDevice MMDevice { get; set; } // Added for volume control
        public override string ToString()
        {
            return ProductName;
        }
    }
}
