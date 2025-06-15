using UnityEngine;
using Fusion;
using Fusion.Photon.Realtime;

public class FusionWebGLFix : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void FixWebGL()
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        // This forces Fusion to use WebSocketSecure before any connection attempt
        Debug.Log("[FusionWebGLFix] Forcing WebSocketSecure for WebGL");
        Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.Protocol = ExitGames.Client.Photon.ConnectionProtocol.WebSocketSecure;
        #endif
    }
}