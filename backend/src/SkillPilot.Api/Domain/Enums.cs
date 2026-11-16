namespace SkillPilot.Api.Domain;

/// <summary>How strong a piece of evidence is. Watching is not the same as doing.</summary>
public enum EvidenceLevel
{
    Watched = 1,
    Understood = 2,
    SolvedWithHints = 3,
    SolvedIndependently = 4,
    AppliedInProject = 5
}

/// <summary>How much AI help was used on an attempt. Derived on the server, never trusted from the client.</summary>
public enum AssistMode
{
    Independent = 0,
    Hint = 1,
    Teacher = 2,
    Solution = 3
}

public enum StudyTaskKind { Learn, Practice, Explain, Revision }

public enum StudyTaskStatus { Planned, Done, Skipped, Missed }

public enum DayMode { Normal, Busy, Minimal, Off }
