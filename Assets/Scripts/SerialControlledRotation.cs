using UnityEngine;

public class SerialControlledRotation : MonoBehaviour
{
    [Header("References")]
    public DataInOut01 serialReader;
    public GameObject targetObject;
    public Light targetLight;

    [Header("Rotation Settings")]
    public float sensitivityX = 0.2f;
    public float sensitivityY = 0.2f;

    private bool lightState = false;
    private int lastButton = 1;

    void Update()
    {
        if (serialReader == null || targetObject == null)
            return;

        int x = serialReader.xAxis;
        int y = serialReader.yAxis;
        int button = serialReader.buttonState;

        // Normalize joystick 0–1023 → -1 to +1
        float normX = (x - 512) / 512f;
        float normY = (y - 512) / 512f;

        // Rotate object
        targetObject.transform.Rotate(-normY * sensitivityY, normX * sensitivityX, 0, Space.Self);

        // Toggle light on press (edge detection)
        if (lastButton == 1 && button == 0)
        {
            lightState = !lightState;
            if (targetLight != null)
                targetLight.enabled = lightState;
        }

        lastButton = button;
    }
}
