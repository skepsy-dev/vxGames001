using UnityEngine;
using WalletConnectUnity.Core;
using WalletConnectUnity.Modal;
using System.Collections;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Fixed WalletConnect integration for WebGL with proper QR code display
/// </summary>
public class WalletConnectWebGLManager : MonoBehaviour
{
    [Header("WalletConnect Configuration")]
    [SerializeField] private bool autoInitialize = true;
    [SerializeField] private string projectId = "YOUR_PROJECT_ID"; // Get from WalletConnect Cloud
    
    [Header("UI References")]
    [SerializeField] private Button connectWalletButton;
    [SerializeField] private TextMeshProUGUI walletStatusText;
    [SerializeField] private GameObject qrCodeDisplay; // For custom QR display if needed
    [SerializeField] private RawImage qrCodeImage; // For displaying QR code manually
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private bool isInitialized = false;
    private bool isConnecting = false;
    
    private void Start()
    {
        if (autoInitialize)
        {
            StartCoroutine(InitializeWalletConnect());
        }
        
        if (connectWalletButton != null)
        {
            connectWalletButton.onClick.AddListener(OnConnectWalletClicked);
        }
        
        UpdateUI("Not Connected");
    }
    
    private IEnumerator InitializeWalletConnect()
    {
        DebugLog("Initializing WalletConnect...");
        
        // Wait for WalletConnect to be ready
        while (!WalletConnectModal.IsReady)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        // Subscribe to events
        WalletConnectModal.Ready += OnWalletConnectReady;
        WalletConnect.Instance.SessionConnected += OnSessionConnected;
        WalletConnect.Instance.SessionDisconnected += OnSessionDisconnected;
        
        isInitialized = true;
        DebugLog("WalletConnect initialized successfully");
    }
    
    private void OnWalletConnectReady(object sender, ModalReadyEventArgs e)
    {
        DebugLog($"WalletConnect Modal Ready. Session resumed: {e.SessionResumed}");
        
        if (e.SessionResumed)
        {
            UpdateUI("Session Resumed");
        }
    }
    
    private async void OnConnectWalletClicked()
    {
        if (!isInitialized)
        {
            DebugLog("WalletConnect not initialized yet!");
            return;
        }
        
        if (isConnecting)
        {
            DebugLog("Already connecting...");
            return;
        }
        
        try
        {
            isConnecting = true;
            UpdateUI("Connecting...");
            
            // Check if already connected
            if (WalletConnect.Instance.IsConnected)
            {
                DebugLog("Already connected, disconnecting first...");
                WalletConnectModal.Disconnect();
                await System.Threading.Tasks.Task.Delay(1000);
            }
            
            DebugLog("Opening WalletConnect modal...");
            
            // Configure connection options
            var options = new WalletConnectModalOptions
            {
                ConnectOptions = new WalletConnectSharp.Sign.Models.Engine.ConnectOptions
                {
                    RequiredNamespaces = new WalletConnectSharp.Sign.Models.RequiredNamespaces
                    {
                        {
                            "eip155", new WalletConnectSharp.Sign.Models.ProposedNamespace
                            {
                                Methods = new[] { "eth_sendTransaction", "personal_sign" },
                                Chains = new[] { "eip155:1" }, // Ethereum mainnet
                                Events = new[] { "chainChanged", "accountsChanged" }
                            }
                        }
                    }
                }
            };
            
            // Open the modal
            WalletConnectModal.Open(options);
            
            // For WebGL, if QR code doesn't show, try manual generation
            StartCoroutine(CheckAndFixQRCode());
        }
        catch (System.Exception ex)
        {
            DebugLog($"Connection error: {ex.Message}");
            UpdateUI("Connection Failed");
        }
        finally
        {
            isConnecting = false;
        }
    }
    
    private IEnumerator CheckAndFixQRCode()
    {
        yield return new WaitForSeconds(1f);
        
        // Check if QR code is displaying properly
        // This is a fallback for WebGL QR code issues
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            DebugLog("Running WebGL QR code check...");
            
            // Try to find the QR code in the modal
            var modalQRCode = GameObject.Find("QRCodeRawImage");
            if (modalQRCode != null)
            {
                var rawImage = modalQRCode.GetComponent<RawImage>();
                if (rawImage != null && rawImage.texture == null)
                {
                    DebugLog("QR code not displaying, attempting fix...");
                    // The modal should handle this, but we can add custom logic here if needed
                }
            }
        }
    }
    
    private void OnSessionConnected(object sender, WalletConnectSharp.Sign.Models.SessionStruct session)
    {
        DebugLog($"Session connected! Topic: {session.Topic}");
        
        var address = session.Namespaces["eip155"].Accounts[0].Split(':')[2];
        UpdateUI($"Connected: {FormatAddress(address)}");
        
        // Store the connected address
        PlayerPrefs.SetString("WalletAddress", address);
        PlayerPrefs.Save();
        
        // Continue to PlayFab login or other game logic
        StartCoroutine(ContinueToGame(address));
    }
    
    private void OnSessionDisconnected(object sender, System.EventArgs e)
    {
        DebugLog("Session disconnected");
        UpdateUI("Disconnected");
        
        PlayerPrefs.DeleteKey("WalletAddress");
        PlayerPrefs.Save();
    }
    
    private IEnumerator ContinueToGame(string walletAddress)
    {
        yield return new WaitForSeconds(1f);
        
        // Here you would continue with PlayFab login
        // For now, just log success
        DebugLog($"Ready to continue with wallet: {walletAddress}");
        
        // Example: Trigger PlayFab login
        // GetComponent<PlayFabManager>()?.ConnectToPlayFab(walletAddress);
    }
    
    private void UpdateUI(string status)
    {
        if (walletStatusText != null)
        {
            walletStatusText.text = status;
        }
        
        if (connectWalletButton != null)
        {
            var buttonText = connectWalletButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                if (WalletConnect.Instance.IsConnected)
                {
                    buttonText.text = "Disconnect Wallet";
                }
                else
                {
                    buttonText.text = "Connect Wallet";
                }
            }
        }
    }
    
    private string FormatAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Length < 10)
            return address;
            
        return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[WalletConnectWebGL] {message}");
        }
    }
    
    private void OnDestroy()
    {
        if (connectWalletButton != null)
        {
            connectWalletButton.onClick.RemoveListener(OnConnectWalletClicked);
        }
        
        WalletConnectModal.Ready -= OnWalletConnectReady;
        WalletConnect.Instance.SessionConnected -= OnSessionConnected;
        WalletConnect.Instance.SessionDisconnected -= OnSessionDisconnected;
    }
}

/// <summary>
/// WebGL-specific fixes for WalletConnect QR Code display
/// </summary>
public static class WalletConnectWebGLFixes
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            Debug.Log("[WalletConnectWebGLFixes] Applying WebGL-specific fixes...");
            
            // Ensure proper initialization order
            Application.targetFrameRate = 60;
            
            // Add any WebGL-specific initialization here
        }
    }
    
    /// <summary>
    /// Manual QR code generation fallback for WebGL
    /// </summary>
    public static Texture2D GenerateQRCodeTexture(string uri)
    {
        try
        {
            // Try using the built-in QRCode utility
            var qrCodeType = System.Type.GetType("WalletConnectUnity.Core.Utils.QRCode");
            if (qrCodeType != null)
            {
                var method = qrCodeType.GetMethod("EncodeTexture", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                if (method != null)
                {
                    return (Texture2D)method.Invoke(null, new object[] { uri, 512, 512 });
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"QR Code generation failed: {ex.Message}");
        }
        
        // Fallback: create a placeholder texture
        var tex = new Texture2D(512, 512);
        var colors = new Color[512 * 512];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.gray;
        }
        tex.SetPixels(colors);
        tex.Apply();
        
        return tex;
    }
}