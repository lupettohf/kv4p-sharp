using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;

namespace RadioControllerApp
{
    public class FormRadio : Form
    {
        private RadioController radioController;
        private WaveInEvent waveIn;
        private BufferedWaveProvider receivedAudioBuffer;
        private WaveOutEvent waveOut;
        private bool isRecording = false;

        // UI Controls
        private TextBox txtPortName;
        private Button btnOpenConnection;
        private Button btnCloseConnection;
        private Button btnInitialize;

        private TextBox txtTXFrequency;
        private TextBox txtRXFrequency;
        private TextBox txtTone;
        private TextBox txtSquelchLevel;
        private Button btnTune;

        private CheckBox chkEmphasis;
        private CheckBox chkHighpass;
        private CheckBox chkLowpass;
        private Button btnSetFilters;

        private Button btnStartRX;
        private Button btnStartTX;
        private Button btnEndTX;
        private Button btnStop;

        private Button btnStartRecording;
        private Button btnStopRecording;
        private Button btnPlayReceivedAudio;

        private TextBox txtStatus;

        public FormRadio()
        {
            InitializeComponent();
            InitializeRadioController();
        }

        private void InitializeComponent()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi; // Enable DPI scaling

            // Form properties
            this.Text = "Radio Controller";
            this.Size = new System.Drawing.Size(800, 700);
            this.MinimumSize = new System.Drawing.Size(600, 600);
            this.FormClosing += FormRadio_FormClosing;

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

            // Adjust row styles
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Connection Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Frequency Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Filter Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Mode Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Audio Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Status TextBox

            // Connection Controls GroupBox
            GroupBox grpConnection = CreateConnectionControls();
            grpConnection.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(grpConnection, 0, 0);

            // Frequency Controls GroupBox
            GroupBox grpFrequency = CreateFrequencyControls();
            grpFrequency.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(grpFrequency, 0, 1);

            // Filter Controls GroupBox
            GroupBox grpFilters = CreateFilterControls();
            grpFilters.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(grpFilters, 0, 2);

            // Mode Controls GroupBox
            GroupBox grpModes = CreateModeControls();
            grpModes.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(grpModes, 0, 3);

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
                ColumnCount = 5,
                AutoSize = true
            };
            grpConnection.Controls.Add(layout);

            Label lblPortName = new Label { Text = "Port Name:", Anchor = AnchorStyles.Right };
            txtPortName = new TextBox { Text = "COM3", Anchor = AnchorStyles.Left };
            btnOpenConnection = new Button { Text = "Open Connection", AutoSize = true };
            btnCloseConnection = new Button { Text = "Close Connection", AutoSize = true };
            btnInitialize = new Button { Text = "Initialize", AutoSize = true };

            btnOpenConnection.Click += btnOpenConnection_Click;
            btnCloseConnection.Click += btnCloseConnection_Click;
            btnInitialize.Click += btnInitialize_Click;

            layout.Controls.Add(lblPortName, 0, 0);
            layout.Controls.Add(txtPortName, 1, 0);
            layout.Controls.Add(btnOpenConnection, 2, 0);
            layout.Controls.Add(btnCloseConnection, 3, 0);
            layout.Controls.Add(btnInitialize, 4, 0);

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Label
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // TextBox
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F)); // Button
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F)); // Button
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F)); // Button

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

        private GroupBox CreateModeControls()
        {
            GroupBox grpModes = new GroupBox
            {
                Text = "Mode Controls",
                AutoSize = true,
                Dock = DockStyle.Fill
            };

            FlowLayoutPanel layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = false,
            };
            grpModes.Controls.Add(layout);

            btnStartRX = new Button { Text = "Start RX Mode", AutoSize = true };
            btnStartTX = new Button { Text = "Start TX Mode", AutoSize = true };
            btnEndTX = new Button { Text = "End TX Mode", AutoSize = true };
            btnStop = new Button { Text = "Stop", AutoSize = true };

            btnStartRX.Click += btnStartRX_Click;
            btnStartTX.Click += btnStartTX_Click;
            btnEndTX.Click += btnEndTX_Click;
            btnStop.Click += btnStop_Click;

            layout.Controls.Add(btnStartRX);
            layout.Controls.Add(btnStartTX);
            layout.Controls.Add(btnEndTX);
            layout.Controls.Add(btnStop);

            return grpModes;
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

            btnStartRecording = new Button { Text = "Start Recording", AutoSize = true };
            btnStopRecording = new Button { Text = "Stop Recording", AutoSize = true };
            btnPlayReceivedAudio = new Button { Text = "Play Received Audio", AutoSize = true };

            btnStartRecording.Click += btnStartRecording_Click;
            btnStopRecording.Click += btnStopRecording_Click;
            btnPlayReceivedAudio.Click += btnPlayReceivedAudio_Click;

            layout.Controls.Add(btnStartRecording);
            layout.Controls.Add(btnStopRecording);
            layout.Controls.Add(btnPlayReceivedAudio);

            return grpAudio;
        }

        private void InitializeRadioController()
        {
            // Disable controls that require an open connection
            ToggleControls(false);

            receivedAudioBuffer = new BufferedWaveProvider(new WaveFormat(44100, 16, 1));
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

                AppendStatus($"Connection opened on {portName}.");
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

        private void btnInitialize_Click(object sender, EventArgs e)
        {
            try
            {
                radioController.Initialize();
                AppendStatus("Radio initialized.");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error initializing radio: {ex.Message}");
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

        private void btnStartRX_Click(object sender, EventArgs e)
        {
            try
            {
                radioController.StartRXMode();
                AppendStatus("Started RX mode.");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error starting RX mode: {ex.Message}");
            }
        }

        private void btnStartTX_Click(object sender, EventArgs e)
        {
            try
            {
                radioController.StartTXMode();
                AppendStatus("Started TX mode.");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error starting TX mode: {ex.Message}");
            }
        }

        private void btnEndTX_Click(object sender, EventArgs e)
        {
            try
            {
                radioController.EndTXMode();
                AppendStatus("Ended TX mode.");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error ending TX mode: {ex.Message}");
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            try
            {
                radioController.Stop();
                AppendStatus("Radio stopped.");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error stopping radio: {ex.Message}");
            }
        }

        private void btnStartRecording_Click(object sender, EventArgs e)
        {
            try
            {
                if (radioController == null)
                {
                    AppendStatus("Please open the connection first.");
                    return;
                }

                radioController.StartTXMode();
                isRecording = true;

                waveIn = new WaveInEvent();
                waveIn.WaveFormat = new WaveFormat(44100, 16, 1);
                waveIn.DataAvailable += WaveIn_DataAvailable;
                waveIn.StartRecording();

                AppendStatus("Recording started.");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error starting recording: {ex.Message}");
            }
        }

        private void btnStopRecording_Click(object sender, EventArgs e)
        {
            try
            {
                if (isRecording && waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                    waveIn = null;
                    isRecording = false;

                    radioController.EndTXMode();
                    AppendStatus("Recording stopped.");
                }
            }
            catch (Exception ex)
            {
                AppendStatus($"Error stopping recording: {ex.Message}");
            }
        }

        private async void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                if (isRecording && radioController != null)
                {
                    byte[] buffer = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, buffer, e.BytesRecorded);

                    // Convert audio data to the required format if necessary
                    await radioController.SendAudioDataAsync(buffer);
                }
            }
            catch (Exception ex)
            {
                AppendStatus($"Error sending audio data: {ex.Message}");
            }
        }

        private void btnPlayReceivedAudio_Click(object sender, EventArgs e)
        {
            try
            {
                if (waveOut == null)
                {
                    waveOut = new WaveOutEvent();
                    waveOut.Init(receivedAudioBuffer);
                }
                waveOut.Play();

                AppendStatus("Playing received audio.");
            }
            catch (Exception ex)
            {
                AppendStatus($"Error playing received audio: {ex.Message}");
            }
        }

        private void RadioController_ErrorOccurred(object sender, ErrorEventArgs e)
        {
            AppendStatus($"Error: {e.GetException().Message}");
        }

        private void RadioController_AudioDataReceived(object sender, byte[] data)
        {
            // Buffer the received audio data
            receivedAudioBuffer.AddSamples(data, 0, data.Length);
            AppendStatus($"Audio data received. Length: {data.Length} bytes.");
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

        private void ToggleControls(bool isEnabled)
        {
            btnInitialize.Enabled = isEnabled;
            btnTune.Enabled = isEnabled;
            btnSetFilters.Enabled = isEnabled;
            btnStartRX.Enabled = isEnabled;
            btnStartTX.Enabled = isEnabled;
            btnEndTX.Enabled = isEnabled;
            btnStop.Enabled = isEnabled;

            btnStartRecording.Enabled = isEnabled;
            btnStopRecording.Enabled = isEnabled;
            btnPlayReceivedAudio.Enabled = isEnabled;

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
        }
    }
}
