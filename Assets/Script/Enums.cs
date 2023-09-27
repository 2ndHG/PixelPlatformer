public class Enums
{
    public bool IsDiagonal(Direction8 direction)
    {
        return direction > Direction8.Down;
    }
}

public enum Direction8
{
    Left, Right, Up, Down, LeftUp, LeftDown, RightUp, RightDown
}