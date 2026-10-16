using Fusion;
using UnityEngine;

public enum InputButton
{
    Jump,
    Revive,
    Grab,
    Throw
}

public struct NetworkInputData : INetworkInput
{
    public Vector2 movementInput;
    public NetworkButtons buttons;
}
