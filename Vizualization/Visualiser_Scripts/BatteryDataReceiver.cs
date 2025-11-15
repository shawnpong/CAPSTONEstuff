using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Security.Cryptography;
using System.IO;
using UnityEngine.UI;
using TMPro;

public class BatteryDataReceiver : MonoBehaviour
{
    // === Server Configuration ===
    private const int PORT = 4211;
    private TcpListener server;
    private Thread serverThread;
    private bool isRunning = false;

    // === AES Configuration ===
    private byte[] aesKey = new byte[] {
        0x2B, 0x7E, 0x15, 0x16, 0x28, 0xAE, 0xD2, 0xA6,
        0xAB, 0xF7, 0x15, 0x88, 0x09, 0xCF, 0x4F, 0x3C
    };

    private byte[] aesIV = new byte[] {
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
        0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F
    };

    // === Battery Data ===
    public float BatteryPercentage { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private Slider batterySlider;
    [SerializeField] private TextMeshProUGUI batteryText;

    private float pendingBatteryValue = -1f;
    private readonly object lockObj = new object();

    void Start()
    {
        StartServer();

        // Initialize slider
        if (batterySlider != null)
        {
            batterySlider.minValue = 0;
            batterySlider.maxValue = 100;
            batterySlider.value = 0;
        }

        // Initialize text
        if (batteryText != null)
        {
            batteryText.text = "Not Connected";
        }
    }

    void StartServer()
    {
        try
        {
            server = new TcpListener(IPAddress.Any, PORT);
            server.Start();
            isRunning = true;

            serverThread = new Thread(ListenForClients);
            serverThread.IsBackground = true;
            serverThread.Start();

            Debug.Log($"Battery Server started on port {PORT}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to start server: {e.Message}");
        }
    }

    void ListenForClients()
    {
        while (isRunning)
        {
            try
            {
                TcpClient client = server.AcceptTcpClient();
                Debug.Log("Laptop connected to BatteryDataReceiver");

                Thread clientThread = new Thread(HandleClientComm);
                clientThread.IsBackground = true;
                clientThread.Start(client);
            }
            catch (Exception e)
            {
                if (isRunning)
                    Debug.LogError($"❌ Accept client failed: {e.Message}");
            }
        }
    }

    void HandleClientComm(object clientObj)
    {
        TcpClient client = (TcpClient)clientObj;
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[4096];
        StringBuilder messageBuilder = new StringBuilder();

        try
        {
            while (isRunning && client.Connected)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuilder.Append(receivedData);

                string fullMessage = messageBuilder.ToString();
                int newlineIndex;

                while ((newlineIndex = fullMessage.IndexOf('\n')) >= 0)
                {
                    string completeMessage = fullMessage.Substring(0, newlineIndex);
                    fullMessage = fullMessage.Substring(newlineIndex + 1);

                    ProcessBatteryMessage(completeMessage.Trim());
                }

                messageBuilder = new StringBuilder(fullMessage);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Client communication error: {e.Message}");
        }
        finally
        {
            client.Close();
            Debug.Log("Client disconnected");
        }
    }

    void ProcessBatteryMessage(string encryptedBase64)
    {
        try
        {
            string decryptedMessage = DecryptAESMessage(encryptedBase64);
            Debug.Log($"🔓 Decrypted: {decryptedMessage}");
            ParseBatteryData(decryptedMessage);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to process battery message: {e.Message}");
        }
    }

    string DecryptAESMessage(string encryptedBase64)
    {
        byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);

        using (Aes aes = Aes.Create())
        {
            aes.Key = aesKey;
            aes.IV = aesIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (ICryptoTransform decryptor = aes.CreateDecryptor())
            using (MemoryStream ms = new MemoryStream(encryptedBytes))
            using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (StreamReader sr = new StreamReader(cs))
            {
                return sr.ReadToEnd();
            }
        }
    }

    void ParseBatteryData(string decryptedMessage)
    {
        // Expected format: "VOLTAGE:3.750,PERCENTAGE:85"
        try
        {
            string[] parts = decryptedMessage.Split(',');
            if (parts.Length == 2 && parts[1].StartsWith("PERCENTAGE:"))
            {
                string percentageStr = parts[1].Substring(11);
                if (float.TryParse(percentageStr, out float percentage))
                {
                    lock (lockObj)
                    {
                        pendingBatteryValue = Mathf.Clamp(percentage, 0f, 100f);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse battery data: {e.Message}");
        }
    }

    void Update()
    {
        lock (lockObj)
        {
            if (pendingBatteryValue >= 0)
            {
                BatteryPercentage = pendingBatteryValue;
                pendingBatteryValue = -1f;
                UpdateBatteryUI();
            }
        }
    }

    void UpdateBatteryUI()
    {
        Debug.Log($"Battery {BatteryPercentage:F1}%");

        if (batterySlider != null)
            batterySlider.value = BatteryPercentage;

        if (batteryText != null)
            batteryText.text = $"{BatteryPercentage:F0}%";
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        server?.Stop();
        serverThread?.Abort();
        Debug.Log("Battery Server stopped");
    }
}
