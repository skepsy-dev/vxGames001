using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Thirdweb;
using Thirdweb.Unity;
using System.Threading.Tasks;
using System;
using System.Numerics;

/// <summary>
/// Basic Web3Manager - Just get WalletConnect working with all wallet options
/// </summary>
public class Web3Manager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private int chainId = 1; // Ethereum mainnet - can be changed to any chain

    [Header("NFT Configuration")]
    private const string KONGZ_VX_CONTRACT = "0x241a81fc0d6692707dad2b5025a3a7cf2cf25acf";
    private const string RONIN_RPC_URL = "https://api-gateway.skymavis.com/rpc";
    private const string RONIN_API_KEY = "bS9xVtjS4fIsT10EoqkfHSO6GhwCpzBt";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // State
    private bool isWalletConnected = false;
    private bool isNFTChecked = false;
    private string walletAddress = "";
    private int nftBalance = 0;
    private IThirdwebWallet connectedWallet;

    // Events for NetworkManager to listen to
    public System.Action<string> OnWalletConnected;
    public System.Action<int> OnNFTBalanceChecked;
    public System.Action<string> OnWeb3Error;
    public System.Action<string> OnConnectionProgress;

    // Public Properties - API Interface
    public bool IsWalletConnected => isWalletConnected;
    public bool IsNFTChecked => isNFTChecked;
    public string GetWalletAddress() => walletAddress;
    public int GetNFTBalance() => nftBalance;
    public bool HasNFTs() => nftBalance > 0;

    /// <summary>
    /// Connect wallet - try multiple approaches to avoid IndexOutOfRangeException
    /// </summary>
    public async Task<bool> ConnectWallet()
    {
        DebugLog("🎯 Starting wallet connection...");

        try
        {
            OnConnectionProgress?.Invoke("Opening wallet selection...");

            // Method 1: Try with Ethereum mainnet (most common)
            DebugLog("📱 Trying Ethereum mainnet...");
            
            var walletOptions = new WalletOptions(
                WalletProvider.WalletConnectWallet, 
                new BigInteger(1) // Ethereum mainnet
            );
            
            connectedWallet = await ThirdwebManager.Instance.ConnectWallet(walletOptions);

            if (connectedWallet != null)
            {
                walletAddress = await connectedWallet.GetAddress();
                isWalletConnected = true;

                DebugLog($"✅ Wallet connected: {walletAddress}");
                OnWalletConnected?.Invoke(walletAddress);
                
                return true;
            }
            else
            {
                throw new Exception("Wallet connection returned null");
            }
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Ethereum connection failed: {ex.Message}");
            
            // Method 2: Try with Polygon
            try
            {
                DebugLog("📱 Trying Polygon...");
                OnConnectionProgress?.Invoke("Trying Polygon network...");
                
                var polygonOptions = new WalletOptions(
                    WalletProvider.WalletConnectWallet, 
                    new BigInteger(137) // Polygon
                );
                
                connectedWallet = await ThirdwebManager.Instance.ConnectWallet(polygonOptions);
                
                if (connectedWallet != null)
                {
                    walletAddress = await connectedWallet.GetAddress();
                    isWalletConnected = true;

                    DebugLog($"✅ Wallet connected (Polygon): {walletAddress}");
                    OnWalletConnected?.Invoke(walletAddress);
                    
                    return true;
                }
            }
            catch (Exception ex2)
            {
                DebugLog($"❌ Polygon connection failed: {ex2.Message}");
            }
            
            // Method 3: Try with Arbitrum
            try
            {
                DebugLog("📱 Trying Arbitrum...");
                OnConnectionProgress?.Invoke("Trying Arbitrum network...");
                
                var arbitrumOptions = new WalletOptions(
                    WalletProvider.WalletConnectWallet, 
                    new BigInteger(42161) // Arbitrum
                );
                
                connectedWallet = await ThirdwebManager.Instance.ConnectWallet(arbitrumOptions);
                
                if (connectedWallet != null)
                {
                    walletAddress = await connectedWallet.GetAddress();
                    isWalletConnected = true;

                    DebugLog($"✅ Wallet connected (Arbitrum): {walletAddress}");
                    OnWalletConnected?.Invoke(walletAddress);
                    
                    return true;
                }
            }
            catch (Exception ex3)
            {
                DebugLog($"❌ Arbitrum connection failed: {ex3.Message}");
            }
            
            // All methods failed
            if (IsUserCancellation(ex))
            {
                OnWeb3Error?.Invoke("Connection cancelled by user");
            }
            else
            {
                OnWeb3Error?.Invoke($"All connection methods failed. Please check WalletConnect configuration.");
            }
            
            return false;
        }
    }

    /// <summary>
    /// Check NFT balance using existing KONGZ VX contract call
    /// </summary>
    public async Task<int> CheckNFTBalance()
    {
        if (!isWalletConnected || string.IsNullOrEmpty(walletAddress))
        {
            DebugLog("❌ Cannot check NFT balance - wallet not connected");
            return 0;
        }

        try
        {
            DebugLog("🎮 Checking NFT balance...");
            OnConnectionProgress?.Invoke("Checking NFTs...");

            // Create the contract call
            string functionSignature = "0x70a08231"; // balanceOf
            string paddedAddress = walletAddress.Replace("0x", "").PadLeft(64, '0');
            string callData = functionSignature + paddedAddress;

            string jsonRequest = CreateEthCallRequest(KONGZ_VX_CONTRACT, callData);

            using (UnityWebRequest request = UnityWebRequest.Post(RONIN_RPC_URL, jsonRequest, "application/json"))
            {
                request.SetRequestHeader("X-API-KEY", RONIN_API_KEY);
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 15;

                var operation = request.SendWebRequest();

                // Wait for completion with timeout
                var startTime = Time.time;
                while (!operation.isDone && (Time.time - startTime) < 15)
                {
                    await Task.Yield();
                }

                if (!operation.isDone)
                {
                    DebugLog("❌ NFT check timed out");
                    request.Abort();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string response = request.downloadHandler.text;
                    nftBalance = ParseBalanceResponse(response);
                    isNFTChecked = true;

                    DebugLog($"✅ NFT balance: {nftBalance} KONGZ VX");
                    OnNFTBalanceChecked?.Invoke(nftBalance);
                    return nftBalance;
                }
                else
                {
                    DebugLog($"❌ NFT check failed: {request.error}");
                    nftBalance = 0;
                    isNFTChecked = true;
                    OnNFTBalanceChecked?.Invoke(0);
                    return 0;
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog($"❌ NFT check error: {ex.Message}");
            nftBalance = 0;
            isNFTChecked = true;
            OnWeb3Error?.Invoke($"NFT check failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Create JSON-RPC request
    /// </summary>
    private string CreateEthCallRequest(string contractAddress, string data)
    {
        return $@"{{
            ""jsonrpc"": ""2.0"",
            ""method"": ""eth_call"",
            ""params"": [
                {{
                    ""to"": ""{contractAddress}"",
                    ""data"": ""{data}""
                }},
                ""latest""
            ],
            ""id"": 1
        }}";
    }

    /// <summary>
    /// Parse NFT balance from RPC response
    /// </summary>
    private int ParseBalanceResponse(string response)
    {
        try
        {
            if (response.Contains("\"result\""))
            {
                int resultStart = response.IndexOf("\"result\":\"") + 10;
                if (resultStart > 9)
                {
                    int resultEnd = response.IndexOf("\"", resultStart);
                    if (resultEnd > resultStart)
                    {
                        string hexResult = response.Substring(resultStart, resultEnd - resultStart);

                        if (hexResult.StartsWith("0x"))
                        {
                            hexResult = hexResult.Substring(2);
                        }

                        if (long.TryParse(hexResult, System.Globalization.NumberStyles.HexNumber, null, out long balance))
                        {
                            return (int)balance;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Failed to parse balance: {ex.Message}");
        }

        return 0;
    }

    /// <summary>
    /// Check if error indicates user cancellation
    /// </summary>
    private bool IsUserCancellation(Exception ex)
    {
        var message = ex.Message.ToLower();
        return message.Contains("user rejected") ||
               message.Contains("user denied") ||
               message.Contains("user cancelled") ||
               message.Contains("user canceled") ||
               message.Contains("rejected by user");
    }

    /// <summary>
    /// Disconnect wallet
    /// </summary>
    public async void DisconnectWallet()
    {
        try
        {
            if (connectedWallet != null)
            {
                await connectedWallet.Disconnect();
            }

            isWalletConnected = false;
            isNFTChecked = false;
            walletAddress = "";
            nftBalance = 0;
            connectedWallet = null;

            DebugLog("✅ Wallet disconnected");
        }
        catch (Exception ex)
        {
            DebugLog($"❌ Disconnect error: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancel connection
    /// </summary>
    public void CancelConnection()
    {
        DebugLog("🛑 Wallet connection cancelled by user");
        OnWeb3Error?.Invoke("Wallet connection cancelled");
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Web3Manager] {message}");
        }
    }
}