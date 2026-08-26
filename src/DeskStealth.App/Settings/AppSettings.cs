namespace DeskStealth.Settings;

internal sealed class AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public int? WindowX { get; set; }

    public int? WindowY { get; set; }

    public float Scale { get; set; } = 1f;

    public bool TopMost { get; set; } = true;

    public int[] BestScores { get; set; } = new int[3];

    public void Normalize()
    {
        Version = CurrentVersion;
        if (Scale is not (1f or 1.25f or 1.5f))
        {
            Scale = 1f;
        }

        if (BestScores is null || BestScores.Length != 3)
        {
            BestScores = new int[3];
        }

        for (var index = 0; index < BestScores.Length; index++)
        {
            BestScores[index] = Math.Max(0, BestScores[index]);
        }
    }
}
