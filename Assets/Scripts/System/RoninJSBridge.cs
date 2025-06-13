using UnityEngine;
using System.Runtime.InteropServices;

public class RoninJSBridge : MonoBehaviour
{
    private static RoninJSBridge _instance;
    public static RoninJSBridge Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("RoninJSBridge");
                _instance = go.AddComponent<RoninJSBridge>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [DllImport("__Internal")]
    private static extern bool DetectRoninWallet();

    [DllImport("__Internal")]
    private static extern void ConnectRoninWallet();

    [DllImport("__Internal")]
    private static extern string GetRoninAddress();

    private Web3Manager web3Manager;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetWeb3Manager(Web3Manager manager)
    {
        web3Manager = manager;
    }

    public void ConnectWallet()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
    if (DetectRoninWallet())
    {
        Debug.Log("Ronin wallet detected, connecting...");
        ConnectRoninWallet();
    }
    else
    {
        Debug.Log("No Ronin wallet detected");
        web3Manager?.OnRoninExtensionError("Ronin wallet extension not found. Please install it.");
    }
#else
        Debug.Log("Not in WebGL build");
#endif
    }

    // Called from JavaScript
    public void OnWalletConnectionSuccess(string address)
    {
        Debug.Log($"Wallet connected successfully: {address}");
        web3Manager?.OnRoninExtensionConnected(address);
    }

    // Called from JavaScript
    public void OnWalletConnectionFailed(string error)
    {
        Debug.Log($"Wallet connection failed: {error}");
        web3Manager?.OnRoninExtensionError(error);
    }


    // Called from JavaScript
    public void OnWalletConnectionRejected()
    {
        Debug.Log("User rejected the connection");
        web3Manager?.OnRoninExtensionError("Connection rejected by user");
    }
}