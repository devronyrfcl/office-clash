using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using System;

public class Spawner : SimulationBehaviour, INetworkRunnerCallbacks
{
    
    [SerializeField] NetworkPlayer networkPlayerPrefab;

    [SerializeField] float fallbackSpawnRadius = 3f;
    
    public void OnConnectedToServer(NetworkRunner runner)
    {
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if(NetworkPlayer.Local != null)
            input.Set(NetworkPlayer.Local.GetNetworkInput());
            
        
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if(runner.IsServer)
        {
            Utils.DebugLog($"Player joined: {player.PlayerId}");

            GetSpawnPose(player, out Vector3 position, out Quaternion rotation);

            runner.Spawn(networkPlayerPrefab, position, rotation, player);
        }
        else
        {
            Utils.DebugLog($"Player joined: {player.PlayerId}");
        }
    }

    //Each player gets their own spawn point, without any the players are spread in a circle
    void GetSpawnPose(PlayerRef player, out Vector3 position, out Quaternion rotation)
    {
        Transform spawnPoint = SpawnPoint.Get(player.PlayerId);

        if(spawnPoint != null)
        {
            position = spawnPoint.position;
            rotation = spawnPoint.rotation;
            return;
        }

        float angle = player.PlayerId * 36f;

        position = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * fallbackSpawnRadius;
        rotation = Quaternion.LookRotation(-position.normalized, Vector3.up);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }
}