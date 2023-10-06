public class Enums
{
    public static bool IsDiagonal(Direction8 direction)
    {
        return direction > Direction8.Down;
    }
    public static bool IsLeftOrRight(Direction8 direction)
    {
        return direction <= Direction8.Right;
    }
    public static bool IsUpOrDown(Direction8 direction)
    {
        return direction >= Direction8.Up && direction <= Direction8.Down;
    }
}

public enum Direction8
{
    Left, Right, Up, Down, LeftUp, LeftDown, RightUp, RightDown
}