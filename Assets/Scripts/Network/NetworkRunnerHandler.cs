using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Threading.Tasks;
using System;
using UnityEngine.SceneManagement;
using System.Linq;

public class NetworkRunnerHandler : MonoBehaviour
{
    [SerializeField] private NetworkRunner networkRunnerPrefab;
    private NetworkRunner networkRunner;

    [Header("Session")]
    [SerializeField] private GameMode gameMode = GameMode.AutoHostOrClient;

    //Leave empty to join any open match (quick match), or set a name to join a specific room
    [SerializeField] private string sessionName = "";

    [SerializeField] private int maxPlayers = 10;


    void Awake()
    {
        networkRunner = FindObjectOfType<NetworkRunner>();
    }


    void Start()
    {
        if (networkRunner == null)
        {
            networkRunner = Instantiate(networkRunnerPrefab);
            networkRunner.name = "Network Runner";
        }

        StartSession();
    }

    async void StartSession()
    {
        string session = string.IsNullOrWhiteSpace(sessionName) ? null : sessionName;

        Utils.DebugLog($"Starting {gameMode} in session {session ?? "(quick match)"}");

        StartGameResult result = await InitializeNetworkRunner(networkRunner, gameMode, session, NetAddress.Any(), SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex), null);

        if(result.Ok)
            Utils.DebugLog($"Joined session {networkRunner.SessionInfo.Name} as {networkRunner.GameMode}");
        else
            Utils.DebugLogError($"Failed to start session: {result.ShutdownReason} {result.ErrorMessage}");
    }

    INetworkSceneManager GetSceneManager(NetworkRunner runner)
    {
        var sceneManager = runner.GetComponents(typeof(MonoBehaviour)).OfType<INetworkSceneManager>().FirstOrDefault();

        if (sceneManager == null)
        {
            sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
        }
        return sceneManager;
    }

    protected virtual Task<StartGameResult> InitializeNetworkRunner(NetworkRunner networkRunner, GameMode gameMode, string sessionName, NetAddress address, SceneRef scene, Action<NetworkRunner> initialized)
    {
        INetworkSceneManager sceneManager = GetSceneManager(networkRunner);

        networkRunner.ProvideInput = true;

        return networkRunner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            Address = address,
            Scene = scene,
            SessionName = sessionName,
            PlayerCount = maxPlayers,
            CustomLobbyName = "OurLobbyID",
            SceneManager = sceneManager,
        });
    }
}