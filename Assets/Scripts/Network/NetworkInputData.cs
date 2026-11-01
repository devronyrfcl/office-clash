using Fusion;
using UnityEngine;

public enum InputButton
{
    Jump,
    Revive,
    Grab,
    Throw,
    ThrowSecond,
    SwapPocket1,
    SwapPocket2
}

public struct NetworkInputData : INetworkInput
{
    public Vector2 movementInput;
    public NetworkButtons buttons;
}
