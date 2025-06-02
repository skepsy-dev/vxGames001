#if CMPSETUP_COMPLETE
using Fusion;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using StarterAssets;
using UnityEngine.InputSystem;

namespace AvocadoShark
{
    public class InGame_Manager : NetworkBehaviour
    {
        public int environment1, environment2;
        public TextMeshProUGUI room_name;
        public TextMeshProUGUI players;
        public Image lock_image;
        public Sprite locked_sprite;
        public TextMeshProUGUI lock_status_text;
        public TextMeshProUGUI InfoText;
        [HideInInspector] public float deltaTime;
        private float fps;
        public int ping;
        public NetworkRunner runner;

        [Header("Game Pause")]
        [SerializeField] private GameObject pauseMenu;

        [SerializeField] private InputActionAsset inputActionAsset;

        [Header("Player Info UI")]
        public TextMeshProUGUI usernameText; // Drag your username text here
        public UnityEngine.UI.Image profilePictureImage; // For later PFP update

        [Header("Character Reference")]
        public CharacterSO characterScriptableObject; // Drag the same CharacterSO asset here

        private void Start()
        {
            if (PlayerPrefs.GetInt("has_pass") == 1)
            {
                lock_image.sprite = locked_sprite;
                lock_status_text.text = "private";
            }
            fps = 1.0f / Time.smoothDeltaTime;
            runner = NetworkRunner.GetRunnerForGameObject(gameObject);

            Debug.Log("🔄 InGame_Manager started, updating player UI...");
            UpdatePlayerUI(); // Update username display and PFP
        }
        public void LeaveRoom()
        {
            var fusionManager = FindFirstObjectByType<FusionConnection>();
            if (fusionManager != null)
            {
                Destroy(fusionManager);
            }
            Runner.Shutdown();
            SceneManager.LoadScene("Menu");
        }
        private float smoothedRTT = 0.0f;
        private void LateUpdate()
        {
            if (!Object)
                return;
            room_name.text = Runner.SessionInfo.Name;
            players.text = Runner.SessionInfo.PlayerCount + "/" + Runner.SessionInfo.MaxPlayers;
            float newFPS = 1.0f / Time.smoothDeltaTime;
            fps = Mathf.Lerp(fps, newFPS, 0.005f);

            double rttInSeconds = runner.GetPlayerRtt(PlayerRef.None);
            int rttInMilliseconds = (int)(rttInSeconds * 1000);
            smoothedRTT = Mathf.Lerp(smoothedRTT, rttInMilliseconds, 0.005f);
            int ping = (int)smoothedRTT / 2;
            InfoText.text = "Ping: " + ping.ToString() + "\n" + "FPS: " + ((int)fps).ToString();
        }

        public void SwitchScene()
        {
            if (!HasStateAuthority)
            {
                SceneSwitchRpc();
                return;
            }
            if (!Runner.IsSceneAuthority)
                return;
            //Assuming envir scene in additive mode is loaded at 1 index
            var environmentSceneIndex = 1;
            var environmentScene = SceneManager.GetSceneAt(environmentSceneIndex);
            print(environmentScene.name);
            var isEnvironment1 = environmentScene.buildIndex == environment1;
            var sceneToLoad = isEnvironment1 ? environment2 : environment1;

            Runner.LoadScene(SceneRef.FromIndex(sceneToLoad), LoadSceneMode.Additive);
            Runner.UnloadScene(SceneRef.FromIndex(environmentScene.buildIndex));
        }

        [Rpc(RpcSources.Proxies, RpcTargets.StateAuthority)]
        private void SceneSwitchRpc()
        {
            SwitchScene();
        }

        public void PauseGame()
        {
            if (FusionConnection.Instance.TryGetLocalPlayerComponent(out StarterAssetsInputs starterAssetsInputs))
            {
                starterAssetsInputs.DisablePlayerInput();
            }

            pauseMenu.SetActive(true);
            //inputActionAsset.Disable();
        }
        public void ResumeGame()
        {
            pauseMenu.SetActive(false);
            //inputActionAsset.Enable();
            if (FusionConnection.Instance.TryGetLocalPlayerComponent(out StarterAssetsInputs starterAssetsInputs))
            {
                starterAssetsInputs.EnablePlayerInput();
            }
        }

        /// <summary>
        /// Update the lobby UI with player information
        /// </summary>
        private void UpdatePlayerUI()
        {
            // Get Web3 username if authenticated
            if (Web3Integration.IsWeb3Authenticated())
            {
                string username = Web3Integration.GetWeb3Username();

                if (usernameText != null)
                {
                    usernameText.text = username;
                    Debug.Log($"🎮 Username displayed in lobby: {username}");
                }
            }
            else
            {
                // Fallback for non-Web3 users
                if (usernameText != null)
                {
                    usernameText.text = "Guest Player";
                }
            }

            // Get the character that was spawned and show its PFP
            UpdateCharacterPFP();
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
        }
        /// <summary>
        /// Update the character profile picture based on spawned character
        /// </summary>
        private void UpdateCharacterPFP()
        {
            // Get the spawned character info from PlayerPrefs
            string spawnedCharacterName = PlayerPrefs.GetString("SpawnedCharacterName", "");
            int spawnedCharacterIndex = PlayerPrefs.GetInt("SpawnedCharacterIndex", 0);

            Debug.Log($"📋 Attempting to update PFP - Name: {spawnedCharacterName}, Index: {spawnedCharacterIndex}");

            if (!string.IsNullOrEmpty(spawnedCharacterName))
            {
                // Use direct reference to CharacterSO instead of finding FusionConnection
                if (characterScriptableObject != null)
                {
                    Debug.Log($"📋 Characters count: {characterScriptableObject.characters.Count}, Index to use: {spawnedCharacterIndex}");

                    if (spawnedCharacterIndex >= 0 && spawnedCharacterIndex < characterScriptableObject.characters.Count)
                    {
                        var spawnedCharacter = characterScriptableObject.characters[spawnedCharacterIndex];

                        Debug.Log($"📋 Found character: {spawnedCharacter.characterName}");

                        if (spawnedCharacter.characterSprite != null && profilePictureImage != null)
                        {
                            Debug.Log($"💾 Setting PFP sprite: {spawnedCharacter.characterSprite.name}");
                            profilePictureImage.sprite = spawnedCharacter.characterSprite;
                            profilePictureImage.color = Color.white; // Ensure full opacity
                            Debug.Log($"🖼️ PFP updated to: {spawnedCharacter.characterName}");
                        }
                        else
                        {
                            Debug.LogWarning($"⚠️ Missing sprite for {spawnedCharacter.characterName} or profilePictureImage not assigned");
                            if (spawnedCharacter.characterSprite == null) Debug.LogWarning("Character sprite is null");
                            if (profilePictureImage == null) Debug.LogWarning("Profile picture image is null");
                        }
                    }
                    else
                    {
                        Debug.LogError($"⚠️ Invalid character index: {spawnedCharacterIndex}, characters count: {characterScriptableObject.characters.Count}");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠️ CharacterSO not assigned to InGame_Manager");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ No spawned character data found for PFP update");
            }
        }

        /// <summary>
        /// Public method to refresh PFP after character spawns
        /// </summary>
        public void RefreshCharacterPFP()
        {
            UpdateCharacterPFP();
        }
    }
}
#endif
