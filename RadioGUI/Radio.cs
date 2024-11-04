using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using System.Collections.Generic;
using System.Collections.Concurrent;

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

        // UI Controls
        private TextBox txtPortName;
        private Button btnOpenConnection;
        private Button btnCloseConnection;

        private TextBox txtTXFrequency;
        private TextBox txtRXFrequency;
        private TextBox txtTone;
        private TextBox txtSquelchLevel;
        private Button btnTune;

        private CheckBox chkEmphasis;
        private CheckBox chkHighpass;
        private CheckBox chkLowpass;
        private Button btnSetFilters;

        private Button btnSetPTTKey;

        private TextBox txtStatus;

        private Panel pnlStatusIndicator;

        public FormRadio()
        {
            InitializeComponent();
            InitializeRadioController();
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
            txtPortName = new TextBox { Text = "COM3", Anchor = AnchorStyles.Left };
            btnOpenConnection = new Button { Text = "Open Connection", AutoSize = true };
            btnCloseConnection = new Button { Text = "Close Connection", AutoSize = true };

            btnOpenConnection.Click += btnOpenConnection_Click;
            btnCloseConnection.Click += btnCloseConnection_Click;

            layout.Controls.Add(lblPortName, 0, 0);
            layout.Controls.Add(txtPortName, 1, 0);
            layout.Controls.Add(btnOpenConnection, 2, 0);
            layout.Controls.Add(btnCloseConnection, 3, 0);

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // TextBox
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
            txtTone = new TextBox { Text = "00", Anchor = AnchorStyles.Left };
            Label lblSquelchLevel = new Label { Text = "Squelch Level:", Anchor = AnchorStyles.Right };
            txtSquelchLevel = new TextBox { Text = "0", Anchor = AnchorStyles.Left };
            btnTune = new Button { Text = "Tune to Frequency", AutoSize = true };

            btnTune.Click += btnTune_Click;

            layout.Controls.Add(lblTXFrequency, 0, 0);
            layout.Controls.Add(txtTXFrequency, 1, 0);
            layout.Controls.Add(lblRXFrequency, 2, 0);
            layout.Controls.Add(txtRXFrequency, 3, 0);
            layout.Controls.Add(lblTone, 4, 0);
            layout.Controls.Add(txtTone, 5, 0);

            layout.Controls.Add(lblSquelchLevel, 0, 1);
            layout.Controls.Add(txtSquelchLevel, 1, 1);
            layout.SetColumnSpan(btnTune, 4);
            layout.Controls.Add(btnTune, 2, 1);

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

            FlowLayoutPanel layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
            };
            grpAudio.Controls.Add(layout);

            btnSetPTTKey = new Button { Text = "Set PTT Key", AutoSize = true };
            btnSetPTTKey.Click += btnSetPTTKey_Click;

            layout.Controls.Add(btnSetPTTKey);

            return grpAudio;
        }

        private void InitializeRadioController()
        {
            // Disable controls that require an open connection
            ToggleControls(false);

            receivedAudioBuffer = new BufferedWaveProvider(new WaveFormat(44100, 8, 1))
            {
                BufferDuration = TimeSpan.FromSeconds(5),
                DiscardOnBufferOverflow = true
            };
            waveOut = new WaveOutEvent();
            waveOut.Init(receivedAudioBuffer);
        }


        private void btnOpenConnection_Click(object sender, EventArgs e)
        {
            try
            {
                string portName = txtPortName.Text.Trim();
                if (string.IsNullOrEmpty(portName))
                {
                    AppendStatus("Please enter a valid port name.");
                    return;
                }

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
                int tone = int.Parse(txtTone.Text.Trim());
                int squelch = int.Parse(txtSquelchLevel.Text.Trim());

                radioController.TuneToFrequency(txFreq, rxFreq, tone, squelch);
                AppendStatus($"Tuned to TX: {txFreq}, RX: {rxFreq}, Tone: {tone}, Squelch Level: {squelch}");
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
                waveIn.WaveFormat = new WaveFormat(44100, 8, 1);
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.StartRecording();

                AppendStatus("Transmission started.");

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
                        txtPortName.Text = line.Substring("PortName=".Length);
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
                        txtTone.Text = line.Substring("Tone=".Length);
                    }
                    else if (line.StartsWith("SquelchLevel="))
                    {
                        txtSquelchLevel.Text = line.Substring("SquelchLevel=".Length);
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
                }
            }
        }

        private void SaveConfigurations()
        {
            string configFile = "config.ini";
            using (StreamWriter writer = new StreamWriter(configFile))
            {
                writer.WriteLine($"PortName={txtPortName.Text}");
                writer.WriteLine($"PTTKey={pttKey}");
                writer.WriteLine($"TXFrequency={txtTXFrequency.Text}");
                writer.WriteLine($"RXFrequency={txtRXFrequency.Text}");
                writer.WriteLine($"Tone={txtTone.Text}");
                writer.WriteLine($"SquelchLevel={txtSquelchLevel.Text}");
                writer.WriteLine($"Emphasis={chkEmphasis.Checked}");
                writer.WriteLine($"Highpass={chkHighpass.Checked}");
                writer.WriteLine($"Lowpass={chkLowpass.Checked}");
            }
        }
    }
}
