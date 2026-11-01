using Cinemachine;
using UnityEngine;

//Created for the local player. Once that player is eliminated it follows the remaining players
public class SpectateCamera : MonoBehaviour
{
    CinemachineVirtualCamera virtualCamera;
    CinemachineBrain brain;

    PlayerHealth target;
    bool seenLocal = false;

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

        if (!spectating)
            return;

        if (target == null || target.IsEliminated)
            target = FindNextTarget(null);
        else if (Input.GetKeyDown(KeyCode.Tab))
            target = FindNextTarget(target);

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
