using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using UnityEngine;

public class DataInOut01 : MonoBehaviour
{
    [Header("Serial Settings")]
    [Tooltip("Leave blank to auto-detect on macOS (usbmodem/usbserial).")]
    public string portName = "/dev/cu.usbmodem2101";
    public int baudRate = 115200;
    [Tooltip("Try to auto-pick first matching macOS serial device if portName is blank or invalid.")]
    public bool autoDetectPort = true;
    [Tooltip("Serial read timeout in ms (used on the background reader thread).")]
    public int readTimeoutMs = 100;

    [Header("Live Data (from Arduino)")]
    public int xAxis = 0;
    public int yAxis = 0;
    public int buttonState = 0; // 0/1 as sent by Arduino (INPUT_PULLUP => pressed==0)

    SerialPort _port;
    Thread _readerThread;
    volatile bool _runReader;
    readonly ConcurrentQueue<string> _lines = new ConcurrentQueue<string>();
    string _lastError = null;

    // NEW: outgoing message queue
    readonly ConcurrentQueue<string> _outgoing = new ConcurrentQueue<string>();

    // --- PUBLIC SEND API (what other scripts will call) ---
    /// <summary>Send a full line to Arduino (a newline is added for you).</summary>
    public void SendLine(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _outgoing.Enqueue(message);
    }

    /// <summary>Convenience formatter: Send("LED {0}", 1);</summary>
    public void Send(string format, params object[] args)
    {
        _outgoing.Enqueue(string.Format(format, args));
    }

    void OnEnable()
    {
        TryOpenPort();
    }

    void OnDisable()
    {
        StopReaderAndClose();
    }

    void Update()
    {
        // Surface any async errors to the Console
        if (!string.IsNullOrEmpty(_lastError))
        {
            Debug.LogError(_lastError);
            _lastError = null;
        }

        // Drain queued lines; parse the most recent valid one
        while (_lines.TryDequeue(out var line))
        {
            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            if (int.TryParse(parts[0], out var xi) &&
                int.TryParse(parts[1], out var yi) &&
                int.TryParse(parts[2], out var bi))
            {
                xAxis = xi;
                yAxis = yi;
                buttonState = bi; // keep raw (0/1). If you want pressed==true: bool pressed = (bi == 0);
            }
        }

        // --- NEW: flush any queued outgoing lines to the serial port ---
        if (_port != null && _port.IsOpen)
        {
            while (_outgoing.TryDequeue(out var line))
            {
                try
                {
                    _port.WriteLine(line);   // appends '\n' based on _port.NewLine
                    // Optionally: Debug.Log($"TX -> {line}");
                }
                catch (System.Exception ex)
                {
                    _lastError = $"Serial write error: {ex.Message}";
                    break; // stop flushing this frame
                }
            }
        }
    }

    void TryOpenPort()
    {
        // Validate/auto-detect port
        if (autoDetectPort)
        {
            var chosen = ChoosePort(portName);
            if (!string.IsNullOrEmpty(chosen)) portName = chosen;
        }

        if (string.IsNullOrEmpty(portName))
        {
            Debug.LogError("No serial port specified/found.");
            return;
        }

        try
        {
            _port = new SerialPort(portName, baudRate);
            _port.NewLine = "\n";           // Arduino println() ends with '\n'
            _port.ReadTimeout = readTimeoutMs;
            _port.DtrEnable = true;         // Often needed for native USB boards
            _port.RtsEnable = true;

            _port.Open();
            _port.DiscardInBuffer();
            Debug.Log($"Opened serial port: {portName} @ {baudRate}");

            // Start background reader
            _runReader = true;
            _readerThread = new Thread(ReaderLoop) { IsBackground = true, Name = "SerialReader" };
            _readerThread.Start();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error opening serial port '{portName}': {ex.Message}");
            SafeClose();
        }
    }

    void ReaderLoop()
    {
        try
        {
            while (_runReader && _port != null && _port.IsOpen)
            {
                try
                {
                    // ReadLine blocks up to ReadTimeout
                    string line = _port.ReadLine(); // expects newline-terminated lines
                    if (!string.IsNullOrWhiteSpace(line))
                        _lines.Enqueue(line);
                }
                catch (TimeoutException)
                {
                    // Normal: just try again
                }
                catch (Exception ex)
                {
                    _lastError = $"Serial read error: {ex.Message}";
                    break;
                }
            }
        }
        finally
        {
            // Reader is exiting; main thread will close port
        }
    }

    void StopReaderAndClose()
    {
        _runReader = false;

        if (_readerThread != null)
        {
            try { _readerThread.Join(500); } catch { /* ignore */ }
            _readerThread = null;
        }

        SafeClose();
    }

    void SafeClose()
    {
        if (_port != null)
        {
            try
            {
                if (_port.IsOpen) _port.Close();
            }
            catch { /* ignore */ }
            finally
            {
                _port.Dispose();
                _port = null;
            }
        }
    }

    static string ChoosePort(string preferred)
    {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        // On macOS, look for a plausible device if preferred is empty or does not exist
        try
        {
            var ports = SerialPort.GetPortNames();
            // If user gave a port and it exists, keep it
            if (!string.IsNullOrEmpty(preferred))
            {
                foreach (var p in ports) if (p == preferred) return preferred;
            }

            // Otherwise pick first usbmodem or usbserial
            foreach (var p in ports)
                if (p.Contains("usbmodem") || p.Contains("usbserial"))
                    return p;

            // Fallback: if any ports exist, return the first
            if (ports.Length > 0) return ports[0];
        }
        catch { /* ignore */ }
#endif
        return preferred; // return what we were given (may be empty)
    }
}