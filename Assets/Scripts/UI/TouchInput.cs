using UnityEngine;

//Where the touch controls put their state. NetworkPlayer reads it together with the keyboard,
//so both work at the same time. Written by UI scripts, read once per network input
public static class TouchInput
{
    const int ButtonCount = 8;

    //Movement from the on-screen stick, length 0 to 1
    public static Vector2 Move { get; set; }

    //Throw aim from dragging off a throw button, length 0 (no aim) or 1
    public static Vector2 Aim { get; set; }

    static readonly bool[] held = new bool[ButtonCount];
    static readonly bool[] tapped = new bool[ButtonCount];

    //For buttons that are held down, for example the throw buttons
    public static void SetHeld(InputButton button, bool isHeld)
    {
        int index = (int)button;

        if (isHeld && !held[index])
            tapped[index] = true;

        held[index] = isHeld;
    }

    //For buttons that are tapped. The tap lives until the next input is sent, so a short tap between two ticks is not lost
    public static void Tap(InputButton button)
    {
        tapped[(int)button] = true;
    }

    //True while the button is held or when it was tapped since the last call
    public static bool Consume(InputButton button)
    {
        int index = (int)button;

        bool result = held[index] || tapped[index];

        tapped[index] = false;

        return result;
    }

    public static void Reset()
    {
        Move = Vector2.zero;
        Aim = Vector2.zero;

        for (int i = 0; i < ButtonCount; i++)
        {
            held[i] = false;
            tapped[i] = false;
        }
    }
}
