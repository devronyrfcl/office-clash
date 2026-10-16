using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 movementInput;
    public NetworkBool isJumpPressed;
    public NetworkBool isRevivePressed;
    public NetworkBool isGrabPressed;
    public NetworkBool isThrowHeld;
}
