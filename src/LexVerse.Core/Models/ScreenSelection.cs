namespace LexVerse.Core.Models;

public sealed record ScreenSelection(int X, int Y, int Width, int Height)
{
    public bool IsMeaningful => Width >= 8 && Height >= 8;
}
