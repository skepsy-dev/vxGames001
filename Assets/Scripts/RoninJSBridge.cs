using System.Runtime.InteropServices;
using UnityEngine;
using System;

/// <summary>
/// JavaScript Bridge for Direct Ronin Wallet Extension Communication
/// Replaces Thirdweb for instant wallet connection
/// </summary>
public class RoninJSBridge : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // Events for Web3Manager to listen to
    public static System.Action<bool> OnRoninDetected;
    public static System.Action<string> OnWalletConnected;
    public static System.Action<string> OnConnectionError;
    public static System.Action<string> OnConnectionProgress;
    
    // Static instance for JavaScript callbacks
    public static RoninJSBridge Instance;
    
    // State
    private bool isRoninDetected = false;
    private bool isConnecting = false;
    private string connectedAddress = "";
    
    // JavaScript function imports (WebGL only)
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern bool DetectRoninWallet();
    
    [DllImport("__Internal")]
    private static extern void ConnectRoninWallet();
    
    [DllImport("__Internal")]
    private static extern string GetRoninAddress();
    
    [DllImport("__Internal")]
    private static extern void SignRoninMessage(string message);
    
    [DllImport("__Internal")]
    private static extern bool IsRoninConnected();
    #endif
    
    private void Awake()
    {
        Instance = this;
        
        #if UNITY_WEBGL && !UNITY_EDITOR
        DebugLog("🌐 RoninJSBridge initialized for WebGL");
        #else
        DebugLog("⚠️ RoninJSBridge - Not WebGL, JavaScript calls will be simulated");
        #endif
    }
    
    private void Start()
    {
        // Detect Ronin wallet on startup
        DetectRonin();
    }
    
    /// <summary>
    /// Check if Ronin wallet extension is available
    /// </summary>
    public bool DetectRonin()
    {
        try
        {
            DebugLog("🔍 Starting Ronin detection from Unity...");
            
            #if UNITY_WEBGL && !UNITY_EDITOR
            isRoninDetected = DetectRoninWallet();
            DebugLog($"📊 JavaScript detection result: {isRoninDetected}");
            #else
            // Simulate detection in editor/non-WebGL
            isRoninDetected = true;
            DebugLog("🎭 Simulating Ronin detection in Editor (always true)");
            #endif
            
            DebugLog(isRoninDetected ? 
                "✅ Ronin Wallet Extension Available!" : 
                "❌ Ronin Wallet Extension Not Found");
            
            OnRoninDetected?.Invoke(isRoninDetected);
            return isRoninDetected;
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Ronin detection failed: {ex.Message}");
            OnRoninDetected?.Invoke(false);
            return false;
        }
    }
    
    /// <summary>
    /// Connect to Ronin wallet (called by Web3Manager)
    /// </summary>
    public void ConnectWallet()
    {
        if (isConnecting)
        {
            DebugLog("⚠️ Connection already in progress, ignoring duplicate request");
            return;
        }
        
        if (!isRoninDetected)
        {
            string error = "Ronin Wallet Extension not detected. Please install Ronin Wallet.";
            DebugLog($"❌ {error}");
            OnConnectionError?.Invoke(error);
            return;
        }
        
        try
        {
            isConnecting = true;
            DebugLog("🚀 Initiating Ronin Wallet connection...");
            OnConnectionProgress?.Invoke("Connecting to Ronin Wallet...");
            
            #if UNITY_WEBGL && !UNITY_EDITOR
            DebugLog("📞 Calling JavaScript ConnectRoninWallet()");
            ConnectRoninWallet();
            #else
            // Simulate connection in editor
            DebugLog("🎭 Simulating Ronin connection in Editor...");
            SimulateEditorConnection();
            #endif
        }
        catch (Exception ex)
        {
            isConnecting = false;
            string error = $"Connection failed: {ex.Message}";
            DebugLog($"❌ {error}");
            OnConnectionError?.Invoke(error);
        }
    }
    
    /// <summary>
    /// Get current wallet address
    /// </summary>
    public string GetWalletAddress()
    {
        try
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            return GetRoninAddress();
            #else
            return connectedAddress;
            #endif
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Failed to get wallet address: {ex.Message}");
            return "";
        }
    }
    
    /// <summary>
    /// Check if wallet is currently connected
    /// </summary>
    public bool IsWalletConnected()
    {
        try
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            return IsRoninConnected();
            #else
            return !string.IsNullOrEmpty(connectedAddress);
            #endif
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Connection check failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Simulate connection for testing in Editor
    /// </summary>
    private void SimulateEditorConnection()
    {
        DebugLog("🎭 Simulating Ronin connection in Editor...");
        
        // Simulate a realistic connection delay
        StartCoroutine(SimulateConnectionDelay());
    }
    
    private System.Collections.IEnumerator SimulateConnectionDelay()
    {
        yield return new UnityEngine.WaitForSeconds(1f);
        
        // Simulate successful connection
        connectedAddress = "0x1234567890123456789012345678901234567890";
        DebugLog($"🎭 Simulated connection success: {connectedAddress}");
        
        OnWalletConnectionSuccess(connectedAddress);
    }
    
    // =============================================================================
    // JAVASCRIPT CALLBACK METHODS - Called from WebGL JavaScript
    // =============================================================================
    
    /// <summary>
    /// Called by JavaScript when wallet connection succeeds
    /// </summary>
    public void OnWalletConnectionSuccess(string walletAddress)
    {
        isConnecting = false;
        connectedAddress = walletAddress;
        
        DebugLog($"✅ Ronin Wallet Connected: {walletAddress}");
        OnConnectionProgress?.Invoke("Ronin Wallet Connected!");
        OnWalletConnected?.Invoke(walletAddress);
    }
    
    /// <summary>
    /// Called by JavaScript when wallet connection fails
    /// </summary>
    public void OnWalletConnectionFailed(string errorMessage)
    {
        isConnecting = false;
        
        DebugLog($"❌ Ronin Wallet Connection Failed: {errorMessage}");
        OnConnectionError?.Invoke($"Connection failed: {errorMessage}");
    }
    
    /// <summary>
    /// Called by JavaScript when user rejects connection
    /// </summary>
    public void OnWalletConnectionRejected()
    {
        isConnecting = false;
        
        DebugLog("🚫 Ronin Wallet Connection Rejected by User");
        OnConnectionError?.Invoke("Connection rejected by user");
    }
    
    /// <summary>
    /// Called by JavaScript with progress updates
    /// </summary>
    public void OnConnectionProgressUpdate(string progressMessage)
    {
        DebugLog($"📊 Connection Progress: {progressMessage}");
        OnConnectionProgress?.Invoke(progressMessage);
    }
    
    /// <summary>
    /// Called by JavaScript when wallet becomes disconnected
    /// </summary>
    public void OnWalletDisconnected()
    {
        connectedAddress = "";
        DebugLog("🔌 Ronin Wallet Disconnected");
    }
    
    /// <summary>
    /// Disconnect wallet
    /// </summary>
    public void DisconnectWallet()
    {
        connectedAddress = "";
        isConnecting = false;
        DebugLog("🔌 Wallet disconnected");
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[RoninJSBridge] {message}");
        }
    }
    
    private void OnDestroy()
    {
        Instance = null;
    }
}