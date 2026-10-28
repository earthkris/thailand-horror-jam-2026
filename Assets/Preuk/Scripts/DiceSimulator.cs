using UnityEngine;

// TEMPORARY stand-in until the real dice system is ready.
// Every roll in the game goes through here, so swapping in the real dice
// later only means changing this one file.
public static class DiceSimulator
{
    // 1 die:  DiceSimulator.Roll(20) = 1d20
    public static int Roll(int sides)
    {
        return Random.Range(1, sides + 1);   // max is exclusive, so +1
    }

    // Several dice added together:  DiceSimulator.Roll(2, 6) = 2d6
    public static int Roll(int count, int sides)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
            total += Roll(sides);
        return total;
    }
}
