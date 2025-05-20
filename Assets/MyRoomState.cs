// MyRoomState.cs
using Colyseus.Schema;

public class MyRoomState : Schema
{
    [Type(0, "number")]
    public double bigNumber; // Matches TypeScript "number"

    [Type(1, "int32")]
    public int regularNumber; // Matches TypeScript "int32"
}

 