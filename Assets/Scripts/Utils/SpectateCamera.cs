using Cinemachine;
using UnityEngine;

//Created for the local player. Once that player is eliminated it follows the remaining players
public class SpectateCamera : MonoBehaviour
{
    CinemachineVirtualCamera virtualCamera;
    CinemachineBrain brain;

    //The spectate buttons read these
    public static bool IsSpectating { get; private set; }
    public static PlayerHealth Target { get; private set; }

    static bool nextRequested = false;

    PlayerHealth target;
    bool seenLocal = false;

    //Called by the Next button, and by the Tab key
    public static void RequestNext()
    {
        nextRequested = true;
    }

    void OnDestroy()
    {
        IsSpectating = false;
        Target = null;
        nextRequested = false;
    }

    void Start()
    {
        virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
        brain = FindObjectOfType<CinemachineBrain>();
    }

    void LateUpdate()
    {
        if (virtualCamera == null)
            return;

        PlayerHealth local = PlayerHealth.Local;

        if (local != null)
            seenLocal = true;

        bool localGone = local == null;
        bool spectating = seenLocal && (localGone || local.IsEliminated);

        IsSpectating = spectating;

        if (!spectating)
        {
            Target = null;
            nextRequested = false;
            return;
        }

        bool wantsNext = nextRequested || Input.GetKeyDown(KeyCode.Tab);
        nextRequested = false;

        if (target == null || target.IsEliminated)
            target = FindNextTarget(null);
        else if (wantsNext)
            target = FindNextTarget(target);

        Target = target;

        if (target != null)
        {
            virtualCamera.m_Follow = target.transform;
            virtualCamera.m_LookAt = target.transform;
        }

        //The local player normally updates the camera, once it is despawned nobody does
        if (localGone && brain != null)
        {
            brain.ManualUpdate();
            virtualCamera.UpdateCameraState(Vector3.up, Time.deltaTime);
        }
    }

    //The next alive player after the current one, or the first alive player
    PlayerHealth FindNextTarget(PlayerHealth current)
    {
        var players = PlayerHealth.All;

        int start = 0;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] == current)
            {
                start = i + 1;
                break;
            }
        }

        for (int i = 0; i < players.Count; i++)
        {
            PlayerHealth candidate = players[(start + i) % players.Count];

            if (!candidate.IsEliminated)
                return candidate;
        }

        return null;
    }
}
