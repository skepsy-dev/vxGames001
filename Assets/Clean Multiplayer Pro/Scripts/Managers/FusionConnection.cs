#if CMPSETUP_COMPLETE
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using StarterAssets;
using Fusion.Photon.Realtime;
using UnityEngine.Serialization;

namespace AvocadoShark
{
    public class FusionConnection : MonoBehaviour, INetworkRunnerCallbacks
    {
        #region ThingsAddedByMe

        public List<PlayerRef> playersInSession = new List<PlayerRef>();

        #endregion

        public RoomEntry CurrentEntryBeingEdited;
        public Action OnDestroyRoomEntries;

        public static FusionConnection Instance;

        [SerializeField] private NetworkRunner runnerPrefab;
        public NetworkRunner Runner { get; private set; }

        public bool hasEnteredGameScene = false;

        [Header("Player 1")][SerializeField] public GameObject playerPrefabYellow;
        [Header("Player 2")][SerializeField] public GameObject playerPrefabRed;
        public CharacterSO characterScriptableObject;
        [Header("Name Entry")] public GameObject mainObject;
        public Button submitButton;
        public TMP_InputField nameField;
        public GameObject characterselectionobject;

        [Header("Room List")] public RoomEntry roomEntryPrefab;
        public GameObject roomListObject;
        public Transform content;
        public Button createRoomButton;
        public TextMeshProUGUI NoRoomsText;
        public TMP_InputField room_search;

        [Header("Room List Refresh (s)")]
        [SerializeField]
        private float refreshInterval = 2f;

        [Header("Player Spawn Location")]
        [SerializeField]
        public bool UseCustomLocation;

        [SerializeField] public Vector3 CustomLocation;

        private FusionVoiceClient _fvc;
        private Recorder _recorder;
        private VoiceManager _voiceManager;

        [Header("Loading Screen")] public LoadingScreen loadingScreenScript;

        [Header("Loading Screen")] public PopUp popup;

        [Header("UI")][SerializeField] private MenuCanvas menuCanvas;

        private bool initialRoomListPopulated = false;
        private List<SessionInfo> _sessionList = new List<SessionInfo>();
        private List<RoomEntry> _roomEntryList = new List<RoomEntry>();

        [HideInInspector] public bool isConnected = false;
        [HideInInspector] public string _playerName = null;
        [HideInInspector] public int nRooms = 0;
        [HideInInspector] public int nPPLOnline = 0;

        public TMP_Dropdown region_select;
        public Button backButton;

        [Header("Web3 Integration")]
        [SerializeField] private bool useWeb3Authentication = true;
        [SerializeField] private TextMeshProUGUI web3StatusText; // Optional: show Web3 status in UI

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            room_search.onValueChanged.AddListener(OnSearchTextValueChange);
#if UNITY_2022_3_OR_NEWER
            Application.targetFrameRate = (int)Screen.currentResolution.refreshRateRatio.value;
#else
    Application.targetFrameRate = Screen.currentResolution.refreshRate;
#endif

            // Region setup (existing code)
            int region_n = PlayerPrefs.GetInt("region");
            string region = null;
            if (region_n == 0) region = "";
            else if (region_n == 1) region = "asia";
            else if (region_n == 2) region = "eu";
            else if (region_n == 3) region = "jp";
            else if (region_n == 4) region = "kr";
            else if (region_n == 5) region = "us";
            region_select.value = region_n;
            PhotonAppSettings settings = Resources.Load<PhotonAppSettings>("PhotonAppSettings");
            settings.AppSettings.FixedRegion = region;

            // NEW: Check for Web3 authentication and auto-setup
            CheckWeb3Authentication();
        }

        public void ChangeRegion(int regionNum)
        {
            string region = null;
            var settings = Resources.Load<PhotonAppSettings>("PhotonAppSettings");
            region = regionNum switch
            {
                0 => "",
                1 => "asia",
                2 => "eu",
                3 => "jp",
                4 => "kr",
                5 => "us",
                _ => null
            };
            settings.AppSettings.FixedRegion = region;
            PlayerPrefs.SetInt("region", regionNum);
        }
        public void RefreshRoomList()
        {
            InitialRoomListSetup();
        }

        private IEnumerator AutoRefreshRoomList()
        {
            while (true)
            {
                RefreshRoomList();
                yield return new WaitForSeconds(refreshInterval);
            }
        }

        public void OnSearchTextValueChange(string value)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value))
            {
                foreach (var i in _roomEntryList)
                {
                    i.gameObject.SetActive(true);
                }
            }

            foreach (var i in _roomEntryList)
            {
                if (value != null && i.roomName.text.Contains(value))
                    i.gameObject.SetActive(true);
                else
                {
                    i.gameObject.SetActive(false);
                }
            }
        }

        public void CreateRoom()
        {
            PlayerPrefs.SetInt("has_pass", 0);
            loadingScreenScript.gameObject.SetActive(true);
            Invoke(nameof(ContinueCreateRoom), loadingScreenScript.lerpSpeed);
        }
        private void ContinueCreateRoom()
        {
            string sessionName = null;
            string sessionPassword = null;
            int maxPlayers = 2;
            if (IsRoomNameValid())
            {
                sessionName = menuCanvas.GetRoomName();
                sessionPassword = menuCanvas.GetPassword();
                maxPlayers = menuCanvas.GetMaxPlayers();
            }
            else
            {
                int randomInt = UnityEngine.Random.Range(1000, 9999);
                sessionPassword = menuCanvas.GetPassword();
                maxPlayers = menuCanvas.GetMaxPlayers();
                sessionName = "Room-" + randomInt;
            }

            Debug.Log($"Session name is {sessionName}");

            Debug.Log($"maxPlayers is {maxPlayers}");

            if (menuCanvas.IsPasswordEnabled)
            {
                PlayerPrefs.SetInt("has_pass", 1);
                JoinRoom(sessionName, maxPlayers, sessionPassword);
            }
            else
            {
                JoinRoom(sessionName, maxPlayers, string.Empty);
            }

            StopCoroutine(AutoRefreshRoomList());
        }

        private bool IsRoomNameValid()
        {
            return menuCanvas.GetRoomName().Length != 0;
        }

        public bool TryGetLocalPlayerComponent<T>(out T component)
        {
            var localPlayer = Runner.GetPlayerObject(Runner.LocalPlayer);
            if (localPlayer == null)
            {
                component = default;
                return false;
            }
            component = localPlayer.GetComponent<T>();
            return component != null;
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (initialRoomListPopulated == false)
            {
                //StartCoroutine(AutoRefreshRoomList());
                loadingScreenScript.FadeOutAndDisable();
            }

            _sessionList = sessionList;
            nRooms = sessionList.Count;

            nPPLOnline = 0;
            foreach (var session in sessionList)
            {
                nPPLOnline += session.PlayerCount;
            }

            RefreshRoomList();
        }

        private void InitialRoomListSetup()
        {
            if (roomListObject == null)
                return;
            initialRoomListPopulated = true;
            roomListObject.SetActive(true);

            OnDestroyRoomEntries?.Invoke();

            //foreach (Transform child in content)
            //{
            //    Destroy(child.gameObject);
            //}

            foreach (SessionInfo session in _sessionList)
            {
                if (CurrentEntryBeingEdited != null)
                {
                    if (session.Name == CurrentEntryBeingEdited.sessionInfo.Name)
                    {
                        UpdateCurrentEntryBeingEdited(session);
                        continue;
                    }
                }

                RoomEntry entryScript = Instantiate(roomEntryPrefab, content);
                entryScript.Init(session, this);
                _roomEntryList.Add(entryScript);
            }

            if (_sessionList.Count == 0)
            {
                NoRoomsText.gameObject.SetActive(true);
            }
            else
            {
                NoRoomsText.gameObject.SetActive(false);
            }
        }

        //public void AddPlayerToBannedList(string name) {
        //runner.SessionInfo.UpdateCustomProperties();
        //}

        public void ConnectToRunner()
        {
            // If Web3 authenticated, use Web3 username
            if (useWeb3Authentication && Web3Integration.IsWeb3Authenticated())
            {
                _playerName = Web3Integration.GetWeb3Username();
                Debug.Log($"🟢 Using Web3 username: {_playerName}");
            }
            else
            {
                // Standard name validation for non-Web3 users
                _playerName = nameField.text;
            }

            loadingScreenScript.gameObject.SetActive(true);
            Invoke(nameof(ContinueConnectToRunner), loadingScreenScript.lerpSpeed);
        }
        private void ContinueConnectToRunner()
        {
            _playerName = nameField.text;
            mainObject.SetActive(false);
            characterselectionobject.SetActive(false);
            SetUpComponents();
            Runner.JoinSessionLobby(SessionLobby.Shared);
        }
        private void SetUpComponents()
        {
            Runner = Instantiate(runnerPrefab);
            _fvc = Runner.GetComponent<FusionVoiceClient>();
            _recorder = Runner.GetComponentInChildren<Recorder>();
            _voiceManager = Runner.GetComponentInChildren<VoiceManager>();
            Runner.AddCallbacks(this);
        }

        // Method 1: Simple join (for joining existing rooms)
        public async void JoinRoom(string sessionName, string password = null)
        {
            int buildIndex = -1;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                // Look for "Lobby" instead of "Game"
                if (sceneName == "Lobby" || sceneName == "Game")
                {
                    buildIndex = i;
                    Debug.Log($"🎯 Found game scene: {sceneName} at index {i}");
                    break;
                }
            }

            // Safety check
            if (buildIndex == -1)
            {
                Debug.LogError("❌ Could not find Lobby or Game scene in build settings!");
                popup.ShowPopup("Scene not found in build settings");
                return;
            }

            StopCoroutine(AutoRefreshRoomList());
            if (Runner == null)
            {
                SetUpComponents();
            }

            Debug.Log($"🚀 Joining room with scene index: {buildIndex}");

            var result = await Runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = sessionName,
                Scene = SceneRef.FromIndex(buildIndex),
            });
            if (result.Ok)
                return;
            popup.ShowPopup(result.ShutdownReason.ToString());
        }

        // Method 2: Create room with max players (for room creation)
        public async void JoinRoom(string sessionName, int maxPlayers, string password = null)
        {
            int buildIndex = -1;

            var sessionProperties = new Dictionary<string, SessionProperty> { { "password", password } };

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                // Look for "Lobby" instead of "Game"
                if (sceneName == "Lobby" || sceneName == "Game")
                {
                    buildIndex = i;
                    Debug.Log($"🎯 Found game scene: {sceneName} at index {i}");
                    break;
                }
            }

            // Safety check
            if (buildIndex == -1)
            {
                Debug.LogError("❌ Could not find Lobby or Game scene in build settings!");
                popup.ShowPopup("Scene not found in build settings");
                return;
            }

            StopCoroutine(AutoRefreshRoomList());

            if (Runner == null)
            {
                SetUpComponents();
            }

            Debug.Log($"🚀 Creating room with scene index: {buildIndex}");

            var result = await Runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = sessionName,
                Scene = SceneRef.FromIndex(buildIndex),
                SessionProperties = sessionProperties,
                PlayerCount = maxPlayers
            });
            if (result.Ok)
            {
                return;
            }
            popup.ShowPopup(result.ShutdownReason.ToString());
        }


        private void SpawnPlayerCharacter(NetworkRunner runner)
        {
            if (runner.GetPlayerObject(runner.LocalPlayer) != null)
                return;

            // Get random character from available prefabs
            var selectedCharacter = characterScriptableObject.GetRandomCharacter();
            var playerPrefab = selectedCharacter.character;

            // STORE the spawned character info for UI display
            int characterIndex = System.Array.IndexOf(characterScriptableObject.characters.ToArray(), selectedCharacter);
            Debug.Log($"💾 Storing character data - Name: {selectedCharacter.characterName}, Index: {characterIndex}");
            PlayerPrefs.SetString("SpawnedCharacterName", selectedCharacter.characterName);
            PlayerPrefs.SetInt("SpawnedCharacterIndex", characterIndex);

            var location = !UseCustomLocation
                ? new Vector3(UnityEngine.Random.Range(-7.6f, 14.2f), 0,
                    UnityEngine.Random.Range(-31.48f, -41.22f))
                : CustomLocation;

            Debug.Log($"🎮 Spawning {selectedCharacter.characterName} at location: {location}");
            var playerObject = runner.Spawn(playerPrefab, location);

            _voiceManager.Init(playerObject.GetComponent<StarterAssetsInputs>(),
                playerObject.GetComponent<PlayerWorldUIManager>());

            runner.SetPlayerObject(runner.LocalPlayer, playerObject.Object);

            Debug.Log($"✅ Player spawned: {selectedCharacter.characterName} - PFP data stored");

            // ADD THIS: Update PFP after character spawns
            var inGameManager = FindFirstObjectByType<InGame_Manager>();
            if (inGameManager != null)
            {
                inGameManager.RefreshCharacterPFP();
            }
        }

        private void CheckWeb3Authentication()
        {
            if (useWeb3Authentication && Web3Integration.IsWeb3Authenticated())
            {
                string web3Username = Web3Integration.GetWeb3Username();
                int nftBalance = Web3Integration.GetWeb3NFTBalance();

                Debug.Log($"🟢 Web3 authenticated user detected: {web3Username} (NFTs: {nftBalance})");

                // Auto-populate name field with Web3 username
                if (nameField != null)
                {
                    nameField.text = web3Username;
                    _playerName = web3Username;
                }

                // Update status text if available
                if (web3StatusText != null)
                {
                    web3StatusText.text = $"✅ {web3Username} | NFTs: {nftBalance}";
                }

                // IMMEDIATELY hide ALL UI elements for Web3 users (no delay)
                if (mainObject != null)
                {
                    mainObject.SetActive(false);
                }

                if (characterselectionobject != null)
                {
                    characterselectionobject.SetActive(false);
                }

                // INSTANTLY show room list and connect (no loading screen delay)
                if (roomListObject != null)
                {
                    roomListObject.SetActive(true);
                }

                // Auto-connect immediately without loading screen
                SetUpComponents();
                Runner.JoinSessionLobby(SessionLobby.Shared);

                Debug.Log("🚀 Web3 user connected instantly - no delays!");
            }
            else
            {
                Debug.Log("🔵 Standard authentication - showing name entry");
                // Show normal name entry UI for non-Web3 users
                if (mainObject != null)
                {
                    mainObject.SetActive(true);
                }
            }
        }

        private void HideLoadingScreen()
        {
            if (loadingScreenScript != null)
            {
                loadingScreenScript.FadeOutAndDisable();
            }
        }


        #region INetworkCallbacks

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("OnConnectedToServer");
            isConnected = true;
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            isConnected = false;
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            popup.ShowPopup(reason.ToString());
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token)
        {
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
            ArraySegment<byte> data)
        {
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            playersInSession.Add(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
        }
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }

            Debug.Log($"🎬 Scene Load Done. Current scene count: {UnityEngine.SceneManagement.SceneManager.sceneCount}");

            // Log all loaded scenes for debugging
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                Debug.Log($"📋 Scene {i}: {scene.name} (buildIndex: {scene.buildIndex})");
            }

            if (hasEnteredGameScene)
                return;
            hasEnteredGameScene = true;

            if (Runner.IsSceneAuthority)
            {
                Debug.Log("🎯 Loading environment scene...");
                // Load Environment 1 (Scene index 3 based on your build settings)
                var asyncOp = Runner.LoadScene(SceneRef.FromIndex(3), LoadSceneMode.Additive);
                asyncOp.AddOnCompleted((op) =>
                {
                    Debug.Log("🌍 Environment loaded, spawning player...");
                    SpawnPlayerCharacter(Runner);
                });
            }
            else
            {
                Debug.Log("🎮 Non-authority player spawning...");
                SpawnPlayerCharacter(Runner);
            }
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }
        #endregion
        public void Checkforname()
        {
            if (nameField.text != "")
                submitButton.interactable = true;
            else
                submitButton.interactable = false;
        }

        public void UpdateCurrentEntryBeingEdited(SessionInfo session)
        {
            CurrentEntryBeingEdited.UpdateEntry(session, this);
        }
        public void SetCurrentEntryBeingEdited(RoomEntry roomEntry)
        {
            if (CurrentEntryBeingEdited != null)
            {
                // If we were previously editing an entry
                OnDestroyRoomEntries += CurrentEntryBeingEdited.DestroyEntry;
                CurrentEntryBeingEdited = roomEntry;
            }
            else
            {
                // If we were not editing any entry
                CurrentEntryBeingEdited = roomEntry;
            }
        }
        public void ResetCurrentEntryBeingEdited(RoomEntry roomEntry)
        {
            if (roomEntry == CurrentEntryBeingEdited)
            {
                OnDestroyRoomEntries += CurrentEntryBeingEdited.DestroyEntry;
                CurrentEntryBeingEdited = null;
            }
        }
    }

    public static class Web3Integration
    {
        public static bool IsWeb3Authenticated()
        {
            return PlayerPrefs.GetString("Web3_AuthComplete", "false") == "true";
        }

        public static string GetWeb3Username()
        {
            return PlayerPrefs.GetString("Web3_Username", "");
        }

        public static string GetWeb3WalletAddress()
        {
            return PlayerPrefs.GetString("Web3_WalletAddress", "");
        }

        public static int GetWeb3NFTBalance()
        {
            return PlayerPrefs.GetInt("Web3_NFTBalance", 0);
        }

        public static bool HasWeb3NFTs()
        {
            return PlayerPrefs.GetInt("Web3_HasNFTs", 0) == 1;
        }

        public static void ClearWeb3Data()
        {
            PlayerPrefs.DeleteKey("Web3_Username");
            PlayerPrefs.DeleteKey("Web3_WalletAddress");
            PlayerPrefs.DeleteKey("Web3_NFTBalance");
            PlayerPrefs.DeleteKey("Web3_HasNFTs");
            PlayerPrefs.DeleteKey("Web3_AuthComplete");
            PlayerPrefs.Save();
        }
    }
}
#endif