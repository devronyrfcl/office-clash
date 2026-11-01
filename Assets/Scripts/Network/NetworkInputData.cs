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
    SwapPocket2,
    ThrowLeft
}

public struct NetworkInputData : INetworkInput
{
    public Vector2 movementInput;
    public NetworkButtons buttons;

    //Direction the player drags from a throw button, same axes as movementInput. Zero means no manual aim
    public Vector2 aimInput;
}
