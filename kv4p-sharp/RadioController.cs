using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

public class RadioController : IDisposable
{
    private SerialPort serialPort;
    private const int baudRate = 921600;
    private const int dataBits = 8;
    private const Parity parity = Parity.None;
    private const StopBits stopBits = StopBits.One;
    private const int readTimeout = 1000;
    private const int writeTimeout = 1000;

    // Delimiter must match ESP32 code
    private static readonly byte[] COMMAND_DELIMITER = new byte[]
    {
        0xFF, 0x00, 0xFF, 0x00,
        0xFF, 0x00, 0xFF, 0x00
    };

    // Command bytes
    private enum ESP32Command : byte
    {
        PTT_DOWN = 1,
        PTT_UP = 2,
        TUNE_TO = 3,
        FILTERS = 4,
        STOP = 5,
        GET_FIRMWARE_VER = 6
    }

    private enum RadioMode
    {
        STARTUP,
        RX,
        TX,
        SCAN
    }

    private RadioMode currentMode = RadioMode.STARTUP;
    private const int MIN_FIRMWARE_VER = 1;
    private string versionStrBuffer = "";

    private const int AUDIO_SAMPLE_RATE = 44100;

    // Synchronization locks
    private readonly object _syncLock = new object();
    private readonly object _versionStrBufferLock = new object();

    /// <summary>
    /// Occurs when an error is encountered.
    /// </summary>
    public event EventHandler<ErrorEventArgs> ErrorOccurred;

    /// <summary>
    /// Occurs when audio data is received in RX mode.
    /// </summary>
    public event EventHandler<byte[]> AudioDataReceived;

    public RadioController(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be null or empty.", nameof(portName));

        serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            ReadTimeout = readTimeout,
            WriteTimeout = writeTimeout
        };
        serialPort.DataReceived += SerialPort_DataReceived;

    }

    public void OpenConnection()
    {
        lock (_syncLock)
        {
            if (!serialPort.IsOpen)
            {
                serialPort.Open();
            }
        }
    }

    public void CloseConnection()
    {
        lock (_syncLock)
        {
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }
        }
    }

    public void Initialize()
    {
        lock (_syncLock)
        {
            currentMode = RadioMode.STARTUP;
        }
        SendCommand(ESP32Command.STOP);
        SendCommand(ESP32Command.GET_FIRMWARE_VER);
    }

    public void StartRXMode()
    {
        lock (_syncLock)
        {
            currentMode = RadioMode.RX;
        }
    }

    public void StartTXMode()
    {
        lock (_syncLock)
        {
            currentMode = RadioMode.TX;
            SendCommand(ESP32Command.PTT_DOWN);
        }
    }

    public void EndTXMode()
    {
        lock (_syncLock)
        {
            if (currentMode == RadioMode.TX)
            {
                SendCommand(ESP32Command.PTT_UP);
                currentMode = RadioMode.RX;
            }
        }
    }

    public void Stop()
    {
        lock (_syncLock)
        {
            currentMode = RadioMode.RX;
        }
        SendCommand(ESP32Command.STOP);
    }

    /// <summary>
    /// Tunes the radio to the specified frequencies with tone and squelch level.
    /// </summary>
    /// <param name="txFrequencyStr">Transmit frequency as a string (e.g., "146.520").</param>
    /// <param name="rxFrequencyStr">Receive frequency as a string (e.g., "146.520").</param>
    /// <param name="tone">Tone value as an integer (00 to 99).</param>
    /// <param name="squelchLevel">Squelch level as an integer (0 to 9).</param>
    public void TuneToFrequency(string txFrequencyStr, string rxFrequencyStr, int tone, int squelchLevel)
    {
        if (string.IsNullOrWhiteSpace(txFrequencyStr))
            throw new ArgumentException("Transmit frequency cannot be null or empty.", nameof(txFrequencyStr));

        if (string.IsNullOrWhiteSpace(rxFrequencyStr))
            throw new ArgumentException("Receive frequency cannot be null or empty.", nameof(rxFrequencyStr));

        txFrequencyStr = MakeSafe2MFreq(txFrequencyStr);
        rxFrequencyStr = MakeSafe2MFreq(rxFrequencyStr);

        string toneStr = tone.ToString("00");
        string squelchStr = squelchLevel.ToString();

        // Ensure squelch level is a single digit
        if (squelchStr.Length != 1)
            throw new ArgumentException("Squelch level must be a single digit (0-9).", nameof(squelchLevel));

        // Build parameters string
        string paramsStr = txFrequencyStr + rxFrequencyStr + toneStr + squelchStr;
        SendCommand(ESP32Command.TUNE_TO, paramsStr);
    }

    public void SetFilters(bool emphasis, bool highpass, bool lowpass)
    {
        string paramsStr = (emphasis ? "1" : "0") + (highpass ? "1" : "0") + (lowpass ? "1" : "0");
        SendCommand(ESP32Command.FILTERS, paramsStr);
    }

    public async Task SendAudioDataAsync(byte[] audioData, int offset, int count, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            if (currentMode != RadioMode.TX)
            {
                return;
            }
        }
        int chunkSize = 512;
        Stopwatch stopwatch = new Stopwatch();

        for (int i = offset; i < count; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int remaining = count - i;
            int size = remaining > chunkSize ? chunkSize : remaining;
            byte[] chunk = new byte[size];
            Array.Copy(audioData, i, chunk, 0, size);

            stopwatch.Restart();
            SendBytesToESP32(chunk);
            stopwatch.Stop();

            // Calculate the expected delay
            double expectedDelay = (double)size / AUDIO_SAMPLE_RATE * 1000.0; // in milliseconds
            double elapsedTime = stopwatch.Elapsed.TotalMilliseconds;
            int delay = (int)(expectedDelay - elapsedTime);

            if (delay > 0)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }
    }


    private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        byte[] receivedData = null;
        lock (_syncLock)
        {
            try
            {
                int bytesToRead = serialPort.BytesToRead;
                receivedData = new byte[bytesToRead];
                serialPort.Read(receivedData, 0, bytesToRead);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs(ex));
            }
        }
        if (receivedData != null && receivedData.Length > 0)
        {
            HandleData(receivedData);
        }
    }

    private void HandleData(byte[] data)
    {
        RadioMode mode;
        lock (_syncLock)
        {
            mode = currentMode;
        }
        if (mode != RadioMode.TX)
        {
            // Raise an event with the received audio data
            OnAudioDataReceived(data);
        }
        else if (mode == RadioMode.STARTUP)
        {
            // Handle firmware version check
            string dataStr = System.Text.Encoding.UTF8.GetString(data);
            lock (_versionStrBufferLock)
            {
                versionStrBuffer += dataStr;
                if (versionStrBuffer.Contains("VERSION"))
                {
                    int startIdx = versionStrBuffer.IndexOf("VERSION") + "VERSION".Length;
                    if (versionStrBuffer.Length >= startIdx + 8)
                    {
                        string verStr = versionStrBuffer.Substring(startIdx, 8);
                        if (int.TryParse(verStr, out int verInt))
                        {
                            if (verInt < MIN_FIRMWARE_VER)
                            {
                                OnErrorOccurred(new ErrorEventArgs(new InvalidOperationException("Unsupported firmware version.")));
                            }
                            else
                            {
                                lock (_syncLock)
                                {
                                    currentMode = RadioMode.RX;
                                }
                                // No need to initialize audio playback
                            }
                        }
                        else
                        {
                            OnErrorOccurred(new ErrorEventArgs(new FormatException("Invalid firmware version format.")));
                        }
                        versionStrBuffer = string.Empty;
                    }
                }
            }
        }
    }

    private void SendCommand(ESP32Command command)
    {
        byte[] commandArray = new byte[COMMAND_DELIMITER.Length + 1];
        Array.Copy(COMMAND_DELIMITER, commandArray, COMMAND_DELIMITER.Length);
        commandArray[COMMAND_DELIMITER.Length] = (byte)command;
        SendBytesToESP32(commandArray);
    }

    private void SendCommand(ESP32Command command, string paramsStr)
    {
        byte[] paramsBytes = System.Text.Encoding.ASCII.GetBytes(paramsStr);
        byte[] commandArray = new byte[COMMAND_DELIMITER.Length + 1 + paramsBytes.Length];
        Array.Copy(COMMAND_DELIMITER, commandArray, COMMAND_DELIMITER.Length);
        commandArray[COMMAND_DELIMITER.Length] = (byte)command;
        Array.Copy(paramsBytes, 0, commandArray, COMMAND_DELIMITER.Length + 1, paramsBytes.Length);
        SendBytesToESP32(commandArray);
    }

    private void SendBytesToESP32(byte[] data)
    {
        lock (_syncLock)
        {
            if (serialPort.IsOpen)
            {
                try
                {
                    serialPort.Write(data, 0, data.Length);
                }
                catch (TimeoutException ex)
                {
                    OnErrorOccurred(new ErrorEventArgs(ex));
                }
                catch (Exception ex)
                {
                    OnErrorOccurred(new ErrorEventArgs(ex));
                }
            }
        }
    }

    private string MakeSafe2MFreq(string strFreq)
    {
        // Implement frequency validation and formatting as needed
        if (!float.TryParse(strFreq, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float freq))
        {
            freq = 146.520f; // Default frequency
        }

        while (freq > 148.0f)
        {
            freq /= 10f;
        }

        freq = Math.Min(freq, 148.0f);
        freq = Math.Max(freq, 144.0f);

        string formattedFreq = freq.ToString("000.000", System.Globalization.CultureInfo.InvariantCulture);
        return formattedFreq;
    }

    protected virtual void OnErrorOccurred(ErrorEventArgs e)
    {
        ErrorOccurred?.Invoke(this, e);
    }

    protected virtual void OnAudioDataReceived(byte[] data)
    {
        AudioDataReceived?.Invoke(this, data);
    }

    #region IDisposable Support

    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                lock (_syncLock)
                {
                    CloseConnection();
                    serialPort?.Dispose();
                    serialPort = null;
                }
            }
            disposedValue = true;
        }
    }

    ~RadioController()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}

