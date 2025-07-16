using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

public class LobbyManagerWindow : EditorWindow
{
    private List<Lobby> lobbies = new List<Lobby>();
    private Vector2 scrollPosition;
    private bool isRefreshing = false;
    private string statusMessage = "";
    private string keyId = "";
    private string secretKey = "";
    private string projectId = "";
    private string environmentId = "";
    private string accessToken = "";
    private const string BASE_URL = "https://lobby.services.api.unity.com/v1";
    private const string AUTH_URL = "https://services.api.unity.com/auth/v1/token-exchange";

    // Analytics
    private DateTime lastRefreshTime;
    private List<LobbyEvent> lobbyEvents = new List<LobbyEvent>();
    private int totalLobbiesDeleted;
    private int maxPlayersSeen;

    // Player view
    private Dictionary<string, List<Player>> lobbyPlayers = new Dictionary<string, List<Player>>();
    private Vector2 playerScrollPosition;
    private string selectedLobbyId;
    private string continuationToken = "";
    private const int LOBBY_LIMIT = 50;

    [MenuItem("Window/Lobby Manager")]
    public static void ShowWindow()
    {
        GetWindow<LobbyManagerWindow>("Lobby Manager");
    }

    private void OnEnable()
    {
        keyId = EditorPrefs.GetString("UnityKeyID", "");
        secretKey = EditorPrefs.GetString("UnitySecretKey", "");
        projectId = EditorPrefs.GetString("UnityProjectID", "");
        environmentId = EditorPrefs.GetString("UnityEnvironmentID", "");

        LoadAnalyticsData();
    }

    private void OnDisable()
    {
        SaveAnalyticsData();
    }

    private void OnGUI()
    {
        GUILayout.Label("Lobby Management", EditorStyles.boldLabel);

        // Service Account Credentials
        GUILayout.Label("Service Account Setup", EditorStyles.label);
        keyId = EditorGUILayout.TextField("Key ID", keyId);
        secretKey = EditorGUILayout.PasswordField("Secret Key", secretKey);
        projectId = EditorGUILayout.TextField("Project ID", projectId);
        environmentId = EditorGUILayout.TextField("Environment ID", environmentId);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Credentials"))
        {
            SaveCredentials();
        }

        if (GUILayout.Button("Switch to Prod", GUILayout.Width(120)))
        {
            environmentId = "production";
            SaveCredentials();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        DrawAnalyticsPanel();

        EditorGUILayout.Space(20);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Authenticate"))
        {
            if (ValidateCredentials())
            {
                Authenticate();
            }
        }

        if (GUILayout.Button("Refresh Lobbies"))
        {
            if (ValidateCredentials() && !string.IsNullOrEmpty(accessToken))
            {
                continuationToken = ""; // Reset pagination
                RefreshLobbies();
            }
            else
            {
                statusMessage = "Please authenticate first";
            }
        }

        if (GUILayout.Button("Load More"))
        {
            if (!string.IsNullOrEmpty(continuationToken)) RefreshLobbies();
        }

        if (GUILayout.Button("Force Delete All"))
        {
            if (EditorUtility.DisplayDialog("Delete ALL Lobbies?",
                "This will delete ALL lobbies in the project. Continue?",
                "Delete", "Cancel"))
            {
                ForceDeleteAllLobbies();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }

        if (isRefreshing)
        {
            EditorGUILayout.LabelField("Refreshing lobbies...");
            return;
        }

        if (lobbies.Count == 0)
        {
            EditorGUILayout.LabelField("No lobbies found");
            return;
        }

        EditorGUILayout.LabelField($"Showing {lobbies.Count} lobbies");
        if (!string.IsNullOrEmpty(continuationToken))
        {
            EditorGUILayout.LabelField("More results available...");
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var lobby in lobbies)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Lobby: {lobby.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"ID: {lobby.id.Substring(0, 8)}...", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            int playerCount = lobby.maxPlayers - lobby.availableSlots;
            EditorGUILayout.LabelField($"Players: {playerCount}/{lobby.maxPlayers}");
            EditorGUILayout.LabelField($"Created: {lobby.created}");

            if (playerCount > maxPlayersSeen)
            {
                maxPlayersSeen = playerCount;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("View Players"))
            {
                selectedLobbyId = lobby.id;
                LoadPlayersForLobby(lobby.id);
            }

            if (GUILayout.Button("Delete"))
            {
                if (EditorUtility.DisplayDialog("Delete Lobby",
                    $"Delete lobby '{lobby.name}'?",
                    "Delete", "Cancel"))
                {
                    DeleteLobby(lobby.id);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();

        DrawPlayerDetails();
    }

    private void DrawAnalyticsPanel()
    {
        GUILayout.Label("Analytics", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField($"Total Lobbies: {lobbies.Count}");
        EditorGUILayout.LabelField($"Max Players Seen: {maxPlayersSeen}");
        EditorGUILayout.LabelField($"Lobbies Deleted: {totalLobbiesDeleted}");
        EditorGUILayout.LabelField($"Last Refresh: {lastRefreshTime.ToString("T")}");

        EditorGUILayout.EndVertical();

        if (GUILayout.Button("Export Analytics Data"))
        {
            ExportAnalyticsData();
        }
    }

    private void DrawPlayerDetails()
    {
        if (string.IsNullOrEmpty(selectedLobbyId) ||
            !lobbyPlayers.ContainsKey(selectedLobbyId))
            return;

        var players = lobbyPlayers[selectedLobbyId];
        if (players == null || players.Count == 0)
            return;

        GUILayout.Label($"Players in Lobby: {selectedLobbyId.Substring(0, 8)}...", EditorStyles.boldLabel);
        playerScrollPosition = EditorGUILayout.BeginScrollView(playerScrollPosition, GUILayout.Height(150));

        foreach (var player in players)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Player ID: {player.id}");

            if (player.data != null)
            {
                foreach (var kvp in player.data)
                {
                    EditorGUILayout.LabelField($"{kvp.Key}: {kvp.Value.value}");
                }
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void SaveCredentials()
    {
        EditorPrefs.SetString("UnityKeyID", keyId);
        EditorPrefs.SetString("UnitySecretKey", secretKey);
        EditorPrefs.SetString("UnityProjectID", projectId);
        EditorPrefs.SetString("UnityEnvironmentID", environmentId);
        statusMessage = "Credentials saved!";
    }

    private bool ValidateCredentials()
    {
        if (string.IsNullOrEmpty(keyId))
        {
            statusMessage = "Key ID is required";
            return false;
        }

        if (string.IsNullOrEmpty(secretKey))
        {
            statusMessage = "Secret Key is required";
            return false;
        }

        if (string.IsNullOrEmpty(projectId))
        {
            statusMessage = "Project ID is required";
            return false;
        }

        if (string.IsNullOrEmpty(environmentId))
        {
            statusMessage = "Environment ID is required";
            return false;
        }

        return true;
    }

    private async void Authenticate()
    {
        isRefreshing = true;
        statusMessage = "Authenticating...";
        Repaint();

        try
        {
            using (var client = new HttpClient())
            {
                var authString = $"{keyId}:{secretKey}";
                var base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                client.DefaultRequestHeaders.Add("Authorization", $"Basic {base64Auth}");

                var authUrl = $"{AUTH_URL}?projectId={projectId}&environmentId={environmentId}";

                var requestData = new TokenExchangeRequest
                {
                    scopes = new string[0]
                };
                var json = JsonUtility.ToJson(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(authUrl, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                var authResponse = JsonUtility.FromJson<TokenExchangeResponse>(responseJson);
                accessToken = authResponse.accessToken;

                statusMessage = "Authenticated successfully!";
                LogLobbyEvent("Authentication", "Success");
            }
        }
        catch (Exception e)
        {
            statusMessage = $"Authentication failed: {e.Message}";
            Debug.LogError(statusMessage);
            LogLobbyEvent("Authentication", $"Failed: {e.Message}");
        }
        finally
        {
            isRefreshing = false;
            Repaint();
        }
    }

    private async void RefreshLobbies()
    {
        isRefreshing = true;
        statusMessage = "Refreshing lobbies...";
        Repaint();

        HttpResponseMessage response = null;
        string responseContent = null;

        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                client.DefaultRequestHeaders.Add("ProjectId", projectId);
                client.DefaultRequestHeaders.Add("Service-Id", "lobby-manager");

                // Create properly structured query request with VALID fields
                var queryRequest = new
                {
                    filter = new object[]
                    {
                    new
                    {
                        field = "AvailableSlots",
                        op = "GT",
                        value = "0"  // Must be string
                    },
                    new
                    {
                        field = "HasPassword",  // Changed from IsPrivate to HasPassword
                        op = "EQ",
                        value = "false"  // Public lobbies don't have passwords
                    }
                    },
                    order = new object[]
                    {
                    new
                    {
                        field = "Created",
                        asc = false
                    }
                    },
                    count = LOBBY_LIMIT,
                    continuationToken = continuationToken
                };

                var jsonRequest = JsonConvert.SerializeObject(queryRequest);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                response = await client.PostAsync($"{BASE_URL}/query", content);
                responseContent = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();

                var lobbyResponse = JsonConvert.DeserializeObject<QueryResponse>(responseContent);

                if (string.IsNullOrEmpty(continuationToken))
                {
                    lobbies = lobbyResponse.results;
                }
                else
                {
                    lobbies.AddRange(lobbyResponse.results);
                }

                continuationToken = lobbyResponse.continuationToken;
                statusMessage = $"Found {lobbies.Count} public lobbies";
                lastRefreshTime = DateTime.Now;
                LogLobbyEvent("Refresh", $"Found {lobbyResponse.results.Count} lobbies");
            }
        }
        catch (Exception e)
        {
            string errorDetails = e.Message;

            if (response != null)
            {
                errorDetails = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {responseContent}";
            }

            statusMessage = $"Error: {errorDetails}";
            Debug.LogError(statusMessage);
            LogLobbyEvent("Refresh", $"Error: {errorDetails}");
        }
        finally
        {
            isRefreshing = false;
            Repaint();
        }
    }

    private async void LoadPlayersForLobby(string lobbyId)
    {
        if (string.IsNullOrEmpty(accessToken)) return;

        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                client.DefaultRequestHeaders.Add("ProjectId", projectId);
                client.DefaultRequestHeaders.Add("Service-Id", "lobby-manager");

                var response = await client.GetAsync($"{BASE_URL}/{lobbyId}");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var lobby = JsonUtility.FromJson<Lobby>(json);

                if (!lobbyPlayers.ContainsKey(lobbyId))
                {
                    lobbyPlayers.Add(lobbyId, new List<Player>());
                }

                lobbyPlayers[lobbyId] = lobby.players;
                LogLobbyEvent("PlayerLoad", $"Loaded {lobby.players.Count} players");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Player load failed: {e.Message}");
        }
    }

    private async void DeleteLobby(string lobbyId)
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                client.DefaultRequestHeaders.Add("ProjectId", projectId);
                client.DefaultRequestHeaders.Add("Service-Id", "lobby-manager");

                var response = await client.DeleteAsync($"{BASE_URL}/{lobbyId}");
                response.EnsureSuccessStatusCode();

                statusMessage = $"Deleted lobby: {lobbyId}";
                totalLobbiesDeleted++;
                LogLobbyEvent("Delete", $"Deleted lobby {lobbyId}");

                // Remove from local list
                lobbies.RemoveAll(l => l.id == lobbyId);
            }
        }
        catch (Exception e)
        {
            statusMessage = $"Delete failed: {e.Message}";
            Debug.LogError(statusMessage);
            LogLobbyEvent("Delete", $"Failed: {e.Message}");
        }
    }

    private async void ForceDeleteAllLobbies()
    {
        if (lobbies.Count == 0) return;

        try
        {
            int deletedCount = 0;
            foreach (var lobby in lobbies.ToList())
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    client.DefaultRequestHeaders.Add("ProjectId", projectId);
                    client.DefaultRequestHeaders.Add("Service-Id", "lobby-manager");

                    var response = await client.DeleteAsync($"{BASE_URL}/{lobby.id}");
                    if (response.IsSuccessStatusCode)
                    {
                        lobbies.Remove(lobby);
                        deletedCount++;
                        totalLobbiesDeleted++;
                    }
                }
            }

            statusMessage = $"Deleted {deletedCount} lobbies";
            LogLobbyEvent("MassDelete", $"Deleted {deletedCount} lobbies");
        }
        catch (Exception e)
        {
            statusMessage = $"Mass delete failed: {e.Message}";
            Debug.LogError(statusMessage);
            LogLobbyEvent("MassDelete", $"Failed: {e.Message}");
        }
    }

    private void LogLobbyEvent(string eventType, string details)
    {
        lobbyEvents.Add(new LobbyEvent
        {
            timestamp = DateTime.Now,
            eventType = eventType,
            details = details,
            lobbyCount = lobbies.Count
        });
    }

    private void SaveAnalyticsData()
    {
        var data = new AnalyticsData
        {
            events = lobbyEvents,
            totalDeletes = totalLobbiesDeleted,
            maxPlayers = maxPlayersSeen
        };

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(Application.dataPath, "LobbyAnalytics.json");
        File.WriteAllText(path, json);
    }

    private void LoadAnalyticsData()
    {
        string path = Path.Combine(Application.dataPath, "LobbyAnalytics.json");
        if (!File.Exists(path)) return;

        string json = File.ReadAllText(path);
        var data = JsonUtility.FromJson<AnalyticsData>(json);

        lobbyEvents = data.events ?? new List<LobbyEvent>();
        totalLobbiesDeleted = data.totalDeletes;
        maxPlayersSeen = data.maxPlayers;
    }

    private void ExportAnalyticsData()
    {
        SaveAnalyticsData();
        string path = Path.Combine(Application.dataPath, "LobbyAnalytics.json");
        EditorUtility.RevealInFinder(path);
    }

    #region API Data Classes
    [System.Serializable]
    private class TokenExchangeRequest
    {
        public string[] scopes;
    }

    [System.Serializable]
    private class TokenExchangeResponse
    {
        public string accessToken;
        public int expiresIn;
    }

    [System.Serializable]
    private class QueryRequest
    {
        public int? count;
        public int? skip;
        public bool sampleResults;
        public List<QueryFilter> filter;
        public List<QueryOrder> order;
        public string continuationToken;
    }

    [System.Serializable]
    private class QueryFilter
    {
        public string field;
        public string op;
        public string value;
    }

    [System.Serializable]
    private class QueryOrder
    {
        public bool asc;
        public string field;
    }

    [System.Serializable]
    private class QueryResponse
    {
        public List<Lobby> results;
        public string continuationToken;
    }

    [System.Serializable]
    private class Lobby
    {
        public string id;
        public string lobbyCode;
        public string upid;
        public string environmentId;
        public string name;
        public int maxPlayers;
        public int availableSlots;
        public bool isPrivate;
        public bool isLocked;
        public bool hasPassword;
        public List<Player> players;
        public Dictionary<string, DataObject> data;
        public string hostId;
        public string created;
        public string lastUpdated;
        public int version;
    }

    [System.Serializable]
    private class Player
    {
        public string id;
        public PlayerProfile profile;
        public string connectionInfo;
        public Dictionary<string, PlayerDataObject> data;
        public string allocationId;
        public string joined;
        public string lastUpdated;
    }

    [System.Serializable]
    private class PlayerProfile
    {
        public string name;
    }

    [System.Serializable]
    private class PlayerDataObject
    {
        public string value;
        public string visibility;
    }

    [System.Serializable]
    private class DataObject
    {
        public string value;
        public string visibility;
        public string index;
    }

    [System.Serializable]
    private class LobbyEvent
    {
        public DateTime timestamp;
        public string eventType;
        public string details;
        public int lobbyCount;
    }

    [System.Serializable]
    private class AnalyticsData
    {
        public List<LobbyEvent> events;
        public int totalDeletes;
        public int maxPlayers;
    }
    #endregion
}