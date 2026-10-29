// How dice amounts are written in menus: what you can get, big,
// and the dice behind it small and faint.   e.g.  4–11 1d8+3
// (TextMeshPro rich text: <size> shrinks it, <alpha> fades it.)
public static class DiceFormat
{
    // "4–11", or "5" when it can only ever be one number
    public static string Range(int min, int max) => min >= max ? min.ToString() : $"{min}–{max}";

    // Small and faint
    public static string Faint(string text) => $"<size=70%><alpha=#66>{text}</size><alpha=#FF>";

    // "4–11" followed by a faint "1d8+3"
    public static string Rich(int min, int max, string dice) => $"{Range(min, max)}  {Faint(dice)}";
}
