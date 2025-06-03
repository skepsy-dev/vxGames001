#if CMPSETUP_COMPLETE
using UnityEngine;
using Fusion;
using TMPro;
using System;
using UnityEngine.SceneManagement;
using System.Collections;
using StarterAssets;

namespace AvocadoShark
{
    public class PlayerStats : NetworkBehaviour
    {
        private ChangeDetector _changeDetector;
        [Networked] public bool IsDisconnecting { get; set; }
        [Networked] public TickTimer VoteTime { get; set; }
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        [Networked] public NetworkString<_32> VoteInitiatorPlayerName { get; set; }
        [Networked] public NetworkBool VoteKick { get; set; }
        [Networked] public int PositiveVotes { get; set; }
        [Networked] public int NegativeVotes { get; set; }

        // NEW: Networked character selection
        [Networked] public int SelectedCharacterIndex { get; set; }

        public int maxVoteTime = 15;

        public bool isVoteInitiator = false;
        public Action<int> OnPositiveVotesChanged, OnNegativeVotesChanged, OnVoteTimeUpdated;
        public Action<bool> OnSpeaking;

        [SerializeField] TextMeshPro playerNameLabel;

        public static PlayerStats instance;

        public Action<string> OnPlayerStatsReady;

        // Character changing references
        private CharacterSO characterSO;
        private ThirdPersonController currentController;

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            GetComponent<PlayerWorldUIManager>().OnSpeaking += Speaking;

            // Get CharacterSO reference ONCE
            var characterSelector = FindFirstObjectByType<CharacterSelector>();
            if (characterSelector != null)
            {
                characterSO = characterSelector.characterScriptableObject;
            }

            currentController = GetComponent<ThirdPersonController>();

            if (HasStateAuthority)
            {
                PlayerName = FusionConnection.Instance._playerName;

                // Initialize with current character selection
                int currentCharacterIndex = PlayerPrefs.GetInt("SpawnedCharacterIndex", 0);
                SelectedCharacterIndex = currentCharacterIndex;

                OnPlayerStatsReady?.Invoke(PlayerName.ToString());
                playerNameLabel.text = !HasStateAuthority ? PlayerName.ToString() : "";
                Debug.Log(PlayerName + " Has state authority");
                if (instance == null)
                {
                    instance = this;
                }
            }
            else
            {
                SessionPlayers.instance.AddPlayer(this);
                playerNameLabel.text = !HasStateAuthority ? PlayerName.ToString() : "";
            }
        }

        public override void Render()
        {
            foreach (var change in _changeDetector.DetectChanges(this, out var previousBuffer, out var currentBuffer))
            {
                switch (change)
                {
                    case nameof(PlayerName):
                        HandleChangeDetection<NetworkString<_32>>(nameof(PlayerName), previousBuffer, currentBuffer,
                            UpdatePlayerName);
                        break;
                    case nameof(VoteKick):
                        HandleChangeDetection<NetworkBool>(nameof(VoteKick), previousBuffer, currentBuffer,
                            OnVoteKickStateChanged);
                        break;
                    case nameof(PositiveVotes):
                        HandleChangeDetection<int>(nameof(PositiveVotes), previousBuffer, currentBuffer,
                            OnPositiveVote);
                        break;
                    case nameof(NegativeVotes):
                        HandleChangeDetection<int>(nameof(NegativeVotes), previousBuffer, currentBuffer,
                            OnNegativeVote);
                        break;
                    case nameof(SelectedCharacterIndex):
                        HandleChangeDetection<int>(nameof(SelectedCharacterIndex), previousBuffer, currentBuffer,
                            OnCharacterIndexChanged);
                        break;
                }
            }
        }

        private void HandleChangeDetection<T>(string propertyName, NetworkBehaviourBuffer previousBuffer,
            NetworkBehaviourBuffer currentBuffer, Action<T, T> callback) where T : unmanaged
        {
            var reader = GetPropertyReader<T>(propertyName);
            var (previous, current) = reader.Read(previousBuffer, currentBuffer);
            callback(previous, current);
        }

        private void Update()
        {
            if (!VoteKick)
                return;
            OnVoteTimeUpdated?.Invoke(Mathf.RoundToInt(VoteTime.RemainingTime(Runner).GetValueOrDefault()));
        }

        public override void FixedUpdateNetwork()
        {
            if (VoteTime.Expired(Runner) && VoteKick)
            {
                VoteKick = false;
            }
        }

        protected void UpdatePlayerName(NetworkString<_32> previous, NetworkString<_32> current)
        {
            SessionPlayers.instance.AddPlayer(this);
            playerNameLabel.text = !HasStateAuthority ? current.ToString() : "";
        }

        // NEW: Handle character index changes from network (SINGLE DEFINITION)
        private void OnCharacterIndexChanged(int previous, int current)
        {
            Debug.Log($"🎭 Character index changed from {previous} to {current}");

            // Update local PlayerPrefs to match network state
            if (characterSO != null && current >= 0 && current < characterSO.characters.Count)
            {
                var character = characterSO.characters[current];
                PlayerPrefs.SetString("SpawnedCharacterName", character.characterName);
                PlayerPrefs.SetInt("SpawnedCharacterIndex", current);

                Debug.Log($"🔄 Updated character data: {character.characterName}");

                // Update the lobby PFP immediately
                var inGameManager = FindFirstObjectByType<InGame_Manager>();
                if (inGameManager != null)
                {
                    inGameManager.RefreshCharacterPFP();
                }

                // NOTE: Real-time prefab swapping disabled for stability
                // Character will change on next spawn instead
            }
        }

        // Existing vote kick methods...
        public void InitializeVoteKick()
        {
            Debug.Log("InitializeVoteKick");

            if (Object.HasStateAuthority)
            {
                PositiveVotes = PositiveVotes + 1;
            }

            if (NotEnoughPlayers())
            {
                Debug.Log("Not enough players for vote kick");
                return;
            }

            if (IsDisconnecting)
            {
                Debug.Log("Player disconnection in process");
                return;
            }

            if (VoteKick == true)
            {
                Debug.Log($"Votekick already in process");
                return;
            }

            if (Runner.GameMode == GameMode.Shared)
            {
                Debug.Log($"Initializing Vote kick for {Runner.GameMode} Mode");
                if (Object.HasStateAuthority)
                {
                    VoteKick = true;
                    VoteInitiatorPlayerName = PlayerName;
                    VoteTime = TickTimer.CreateFromSeconds(Runner, maxVoteTime);
                }
                else
                {
                    RPC_BeginVoteKick();
                    isVoteInitiator = true;
                }
            }
            else
            {
                Debug.Log($"Initializing Vote kick for {Runner.GameMode} Mode");
            }
        }

        public void OnVoteKickStateChanged(NetworkBool previous, NetworkBool current)
        {
            Debug.Log("Vote kick state changed");
            Debug.Log($"positive votes {PositiveVotes}");
            if (current)
            {
                SessionPlayers.instance.AddVoteKick(this);
                PositiveVotes = 0;
                NegativeVotes = 0;
            }
            else
            {
                if (HasStateAuthority)
                {
                    if (PositiveVotes > NegativeVotes)
                    {
                        IsDisconnecting = true;
                        RemovePlayer();
                        RPC_PlayerVoteResultMessage(
                            $"Vote kick for {PlayerName} has passed");
                    }
                    else
                    {
                        RPC_PlayerVoteResultMessage(
                            $"Vote kick for {PlayerName} has failed");
                    }

                    PositiveVotes = 0;
                    NegativeVotes = 0;
                }

                SessionPlayers.instance.RemoveVoteKick(this);
            }
        }

        public int GetNegativeVotes()
        {
            return (SessionPlayers.instance.activePlayers.Count - PositiveVotes - 1);
        }

        public bool NotEnoughPlayers()
        {
            return (SessionPlayers.instance.activePlayers.Count <= 2);
        }

        public void AddPositiveVote()
        {
            if (!VoteKick)
                return;
            if (Object.HasStateAuthority)
                PositiveVotes += 1;
            else
                RPC_AddPositiveVote();
        }

        public void AddNegativeVote()
        {
            if (!VoteKick)
                return;
            if (Object.HasStateAuthority)
                NegativeVotes += 1;
            else
                RPC_AddNegativeVote();
        }

        public void RemovePlayer()
        {
            if (Object.HasStateAuthority)
            {
                Debug.Log("Shutting Down");
                StartCoroutine(RemovePlayerAfterDelay(3f));
            }
        }

        private IEnumerator RemovePlayerAfterDelay(float time)
        {
            yield return new WaitForSeconds(time);
            Runner.Shutdown();
            SceneManager.LoadScene(0);
        }

        public void OnPositiveVote(int previous, int current)
        {
            OnPositiveVotesChanged?.Invoke(current);
        }

        public void OnNegativeVote(int previous, int current)
        {
            OnNegativeVotesChanged?.Invoke(current);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            SessionPlayers.instance.RemovePlayer(this);
            if (VoteKick)
            {
                SessionPlayers.instance.RemoveVoteKick(this);
                RPC_PlayerVoteResultMessage($"Vote kick failed");
            }
        }

        // RPC Methods
        [Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority)]
        public void RPC_BeginVoteKick()
        {
            Debug.Log("RPC_BeginVoteKick");
            VoteKick = true;
            VoteTime = TickTimer.CreateFromSeconds(Runner, maxVoteTime);
        }

        [Rpc(sources: RpcSources.StateAuthority, targets: RpcTargets.All)]
        public void RPC_PlayerVoteResultMessage(string message)
        {
            Debug.Log("RPC_PlayerVoteResultMessage");
            InfoPanel.instance.AddMessage(message);
        }

        [Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority)]
        public void RPC_AddPositiveVote()
        {
            PositiveVotes += 1;
        }

        [Rpc(sources: RpcSources.Proxies, targets: RpcTargets.StateAuthority)]
        public void RPC_AddNegativeVote()
        {
            Debug.Log("RPC_AddNegativeVote");
            NegativeVotes += 1;
        }

        // NEW: RPC for character changes (just updates display, not prefab)
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_ChangeCharacter(int characterIndex)
        {
            if (characterSO != null && characterIndex >= 0 && characterIndex < characterSO.characters.Count)
            {
                var selectedCharacter = characterSO.characters[characterIndex];

                // Store the character data
                PlayerPrefs.SetString("SpawnedCharacterName", selectedCharacter.characterName);
                PlayerPrefs.SetInt("SpawnedCharacterIndex", characterIndex);

                Debug.Log($"🎭 Character changed to: {selectedCharacter.characterName} (index: {characterIndex})");

                // Update the lobby PFP
                var inGameManager = FindFirstObjectByType<InGame_Manager>();
                if (inGameManager != null)
                {
                    inGameManager.RefreshCharacterPFP();
                }
            }
        }

        // NEW: RPC for real-time character prefab changes
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ChangeCharacterPrefab(int characterIndex)
        {
            if (Object.HasStateAuthority)
            {
                Debug.Log($"🌐 Authority received character change request: {characterIndex}");
                SelectedCharacterIndex = characterIndex;
            }
        }

        private void Speaking(bool value)
        {
            OnSpeaking?.Invoke(value);
        }

        // Add these methods to your existing PlayerStats.cs class:

        // Add this RPC method to handle character respawn requests
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestCharacterRespawn()
        {
            if (Object.HasStateAuthority)
            {
                Debug.Log($"🔄 Character respawn requested - despawning and respawning player");
                StartCoroutine(RespawnWithNewCharacter());
            }
        }

        // Coroutine to handle the respawn process
        private IEnumerator RespawnWithNewCharacter()
        {
            // Store current position and rotation
            Vector3 currentPosition = transform.position;
            Quaternion currentRotation = transform.rotation;

            // Get the FusionConnection instance
            var fusionConnection = FindFirstObjectByType<FusionConnection>();
            if (fusionConnection == null)
            {
                Debug.LogError("❌ FusionConnection not found!");
                yield break;
            }

            // Despawn current player object
            Debug.Log("🔴 Despawning current player...");
            Runner.Despawn(Object);

            // Wait a frame to ensure despawn completes
            yield return null;

            // Spawn new character with updated selection
            Debug.Log("🟢 Spawning new character...");

            // The CharacterSO will use the updated selection from PlayerPrefs
            var characterSO = fusionConnection.characterScriptableObject;
            var selectedCharacter = characterSO.GetSelectedCharacter();
            var playerPrefab = selectedCharacter.character;

            // Store the spawned character info
            int characterIndex = characterSO.GetSelectedCharacterIndex;
            PlayerPrefs.SetString("SpawnedCharacterName", selectedCharacter.characterName);
            PlayerPrefs.SetInt("SpawnedCharacterIndex", characterIndex);

            Debug.Log($"🎮 Spawning {selectedCharacter.characterName} at previous location");

            // Spawn the new character
            var playerObject = Runner.Spawn(playerPrefab, currentPosition, currentRotation);

            // Set up voice manager if needed
            var voiceManager = FindFirstObjectByType<VoiceManager>();
            if (voiceManager != null)
            {
                var starterInputs = playerObject.GetComponent<StarterAssetsInputs>();
                var worldUIManager = playerObject.GetComponent<PlayerWorldUIManager>();
                if (starterInputs != null && worldUIManager != null)
                {
                    voiceManager.Init(starterInputs, worldUIManager);
                }
            }

            // Set as player object
            Runner.SetPlayerObject(Runner.LocalPlayer, playerObject.Object);

            Debug.Log($"✅ Character respawned as: {selectedCharacter.characterName}");

            // Update the lobby PFP
            var inGameManager = FindFirstObjectByType<InGame_Manager>();
            if (inGameManager != null)
            {
                // Small delay to ensure everything is set up
                yield return new WaitForSeconds(0.1f);
                inGameManager.RefreshCharacterPFP();
            }
        }

    }
}
#endif