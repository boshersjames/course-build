// Hole labels for multi-nine clubs. A GKD whose CourseName names its nines after a " - ", e.g.
// "Moorpark - Ridgeline / Creekside" (18 holes) or "Moorpark - Canyon Crest" (9 holes), gets holes called
// "Ridgeline 1" .. "Ridgeline 9", "Creekside 1" ..; any other course keeps "Hole 1" .. "Hole 18".
using System;

public static class CourseHoleNames
{
    public static string[] Nines(string courseName)
    {
        if (string.IsNullOrEmpty(courseName)) return new string[0];
        int i = courseName.IndexOf(" - ", StringComparison.Ordinal);
        if (i < 0) return new string[0];
        string[] parts = courseName.Substring(i + 3).Split('/');
        for (int k = 0; k < parts.Length; k++) parts[k] = parts[k].Trim();
        return parts;
    }

    // holeNumber is 1-based, as in the GKD
    public static string Label(string courseName, int holeNumber, int holeCount)
    {
        string[] nines = Nines(courseName);
        if (nines.Length == 1 && holeNumber >= 1 && holeNumber <= 9)      // a single nine (its GKD keeps 18 holes, back nine off)
            return nines[0] + " " + holeNumber;
        if (nines.Length > 0 && holeCount == nines.Length * 9 && holeNumber >= 1 && holeNumber <= holeCount)
            return nines[(holeNumber - 1) / 9] + " " + ((holeNumber - 1) % 9 + 1);
        return "Hole " + holeNumber;
    }

    // for file names: "Ridgeline_1", or "hole01" as before
    public static string FileTag(string courseName, int holeNumber, int holeCount)
    {
        string l = Label(courseName, holeNumber, holeCount);
        return l.StartsWith("Hole ") ? "hole" + holeNumber.ToString("00") : l.Replace(' ', '_');
    }
}
