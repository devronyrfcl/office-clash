using System;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] NetworkRunner runnerPrefab;
    [SerializeField] Button quickMatchButton;
    [SerializeField] TMP_Text statusText;
    [SerializeField] string officeScenePath = "Assets/Scenes/Office.unity";
    [SerializeField] int maxPlayers = 10;

    bool connecting;

    void Awake()
    {
        quickMatchButton.onClick.AddListener(QuickMatch);
    }

    void OnDestroy()
    {
        if (quickMatchButton != null)
            quickMatchButton.onClick.RemoveListener(QuickMatch);
    }

    public async void QuickMatch()
    {
        if (connecting)
            return;

        int sceneIndex = SceneUtility.GetBuildIndexByScenePath(officeScenePath);
        if (runnerPrefab == null || sceneIndex < 0)
        {
            statusText.text = "Unable to start a match. Check the game setup.";
            Debug.LogError("Quick Match requires a runner prefab and the Office scene in Build Settings.");
            return;
        }

        connecting = true;
        quickMatchButton.interactable = false;
        statusText.text = "CONNECTING...";
        NetworkRunner runner = null;

        try
        {
            runner = Instantiate(runnerPrefab);
            runner.name = "Network Runner";
            DontDestroyOnLoad(runner.gameObject);
            runner.ProvideInput = true;
            var sceneManager = runner.GetComponent<NetworkSceneManagerDefault>();
            if (sceneManager == null)
                sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                Address = NetAddress.Any(),
                SessionName = null,
                PlayerCount = maxPlayers,
                CustomLobbyName = "OurLobbyID",
                Scene = SceneRef.FromIndex(sceneIndex),
                SceneManager = sceneManager
            });

            if (result.Ok)
                return; // Fusion loads Office and owns the runner from here.

            Debug.LogError($"Quick Match failed: {result.ShutdownReason} {result.ErrorMessage}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }

        if (runner != null)
            Destroy(runner.gameObject);

        if (this != null)
        {
            connecting = false;
            quickMatchButton.interactable = true;
            statusText.text = "COULD NOT CONNECT. PLEASE TRY AGAIN.";
        }
    }
}
