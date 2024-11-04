# KV4P-Sharp

KV4P-Sharp is a C# library designed to communicate with KV4P USB VHF radios. It facilitates control over radio operations through serial communication but does not handle audio encoding or decoding. Users must manage audio processing separately by encoding/decoding audio data as PCM 44100Hz, 8-bit mono.

## Features
- Control transmit (PTT) and receive (RX) modes.
- Set and tune to specific frequencies with tone and squelch level settings.
- Configure audio filters (emphasis, high-pass, low-pass).
- Check firmware version for compatibility.
- Event-based handling of audio data and error notifications.

## Example Application

The repository includes a sample application called `RadioGUI` that demonstrates basic usage of the KV4P-Sharp library, showcasing how to control a KV4P radio using a simple GUI interface.

## Installation

1. Clone the repository.
2. Include the KV4P-Sharp library in your C# project.
3. Connect a KV4P USB VHF radio via serial port.

## Usage

To interact with a KV4P radio:

```csharp
using System;
using KV4PSharp;

public class Example
{
    public static void Main()
    {
        using (var radio = new RadioController("COM3")) // Replace with your port name
        {
            radio.OpenConnection();
            radio.Initialize();
            
            radio.TuneToFrequency("146.520", "146.520", 0, 5); // Example frequency
            radio.StartRXMode();

            // Handle received audio data
            radio.AudioDataReceived += (sender, data) =>
            {
                // Process received PCM 44100Hz 8-bit mono data
            };

            // Clean up
            radio.CloseConnection();
        }
    }
}
```
## Important Note on Audio

KV4P-Sharp **does not process audio data**. Users are responsible for encoding or decoding audio to and from PCM format (44100Hz, 8-bit mono) when transmitting or receiving data. See RadioGUI as example.

