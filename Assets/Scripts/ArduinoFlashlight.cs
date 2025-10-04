using UnityEngine;
using System;
using System.IO.Ports;

public class ArduinoFlashlight : MonoBehaviour
{
    public string portName = "/dev/cu.usbmodem2101";
    public int baudRate = 9600;
    public float rotationSpeed = 1f;

    private SerialPort serialPort;
    private Light flashlight;

    void Start()
    {
        flashlight = GetComponent<Light>();
        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.NewLine = "\n"; // <--- CHANGE 1: Explicitly set NewLine to what Serial.println() uses.
            serialPort.ReadTimeout = 100;
            serialPort.Open();
            
            serialPort.ReadExisting();
            
            Debug.Log("✅ Serial port opened: " + portName);
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Failed to open serial port: " + e.Message);
        }
    }

    void Update()
{
    if (serialPort != null && serialPort.IsOpen)
    {
        try
        {
            // CHANGE 2: Removed BytesToRead check. ReadLine() blocks (up to ReadTimeout) until a NewLine is found.
            // Reading only when BytesToRead > 0 can sometimes lead to partial reads or timing issues.
            string data = serialPort.ReadLine();
            Debug.Log("📩 Received: " + data);

            string[] parts = data.Split(',');
            if (parts.Length == 3)
            {
                int xVal = int.Parse(parts[0]);
                int yVal = int.Parse(parts[1]);
                int switchState = int.Parse(parts[2]);

                float xNorm = (xVal - 512f) / 512f;
                float yNorm = (yVal - 512f) / 512f;

                transform.Rotate(Vector3.up, xNorm);
                transform.Rotate(Vector3.right, -yNorm);

                flashlight.enabled = (switchState == 1);
            }
        }
        catch (TimeoutException)
        {
            // fine — means no new data
        }
        catch (Exception e)
        {
            Debug.LogWarning("Serial read error: " + e.Message);
        }
    }
}


    void OnApplicationQuit()
    {
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            Debug.Log("🔒 Serial port closed.");
        }
    }
}