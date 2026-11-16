# Design notes

## Principles

1. The app owns your learning history, not an AI chat. Changing AI provider loses nothing.
2. Progress is evidence, not time spent or boxes ticked.
3. A missed day is normal. The plan continues from real progress and never restarts.
4. The AI teaches and checks. It does not schedule. Scheduling is plain code that can be tested.
5. Keep it small: one API, one database, one AI interface.

## Mastery (backend/src/SkillPilot.Api/Core/MasteryCalculator.cs)

Each piece of evidence has a level and an amount of AI help.

| Level | Meaning | Highest mastery it allows |
| --- | --- | --- |
| Watched | read the lesson, or saw the solution | 25 |
| Understood | explained it, or unverified practice | 45 |
| Solved with hints | partly right, or used hints or the tutor | 65 |
| Solved independently | solved alone and scored 70 or more | 90 |
| Applied in a project | used in a real project (planned) | 100 |

- Help lowers the level: viewing the solution counts as Watched, using the tutor or hints caps at Solved with hints.
- Score (0 to 100) scales the credit. Scores under 50 do not raise the cap.
- Evidence fades with a half life of 45 days. Several good results combine with diminishing returns.
- A skill's mastery is the average of its topics, and untouched topics count as zero.
- 70 or more is "solid". Self confidence (0 to 10) is stored separately and the difference is shown as a gap.

## Planner (Core/StudyPlanner.cs)

- Looks 7 days ahead from today in the user's timezone.
- A day's budget is that weekday's available minutes. Busy caps today at 20, Minimal at 10, Day off at 0. Minutes already studied today are subtracted. Tomorrow is not touched.
- Each topic has steps: Learn, Practice (if it has code), Explain. If all are done but mastery is under 70, one Revision step is added.
- Two lanes: the first priority skill (60 percent) and DSA (40 percent). If DSA is the first priority, the lanes are DSA and the next skill.
- A spaced revision comes first on the day it is due (one a day, at most 35 percent of the time). Intervals grow 1, 3, 7, 14, 30 days after good results; a weak result returns the next day.
- A task is shortened to fit the time left (at least 10 minutes) or left for another day. Nothing is scheduled over budget.
- Mastered topics (70 or more) are skipped, so returning to DSA never restarts at arrays.
- Opening the plan marks tasks from earlier days as moved, rebuilds the future from the real state and records why in the audit trail.

## AI (Ai/)

- `IAiProvider` has two implementations (Anthropic Messages API, OpenAI chat completions) using plain HttpClient.
- `TutorService` picks the provider from the user's setting, enforces a daily call limit, builds prompts and parses JSON safely.
- If no key is set or a call fails, `OfflineTutor` answers and says so. Its code reviews are marked unverified and can only earn "understood" credit.
- Only a short summary of the learner is sent (strong topics, weak topics, minutes this week), never the full history.
- Learner text is wrapped in `<learner_input>` tags and the system prompt says to treat it as data, which reduces prompt injection. It does not remove it, so scores from the AI are advice with a bounded effect (capped by the evidence rules).
- Hints and the sample solution are created together with the exercise and stored on the server. The solution is not sent to the browser until the user has made an attempt and asks for it.
- The help level is worked out on the server from what was actually used.

## Security

- JWT bearer auth, PBKDF2 password hashing, the same error for unknown email and wrong password.
- Every query is scoped by the signed in user's id. Tests check that another user gets 404.
- Registration is closed unless the email is on `Registration:AllowedEmails` (open only in Development).
- Rate limits: 10 auth requests per minute per IP, 240 requests per minute overall.
- Provider errors never include response bodies, so keys and prompts cannot leak to logs.
- No secrets in the frontend or in source control.

## Data model (short)

User, UserProfile, UserSkill (confidence), Evidence, StudyTask, StudySession, Exercise, Submission, PlanChange (audit), DayState, AiUsage. The skill and topic catalog is code (`Core/Curriculum.cs`) and is copied to the database at start.

## Next milestones

1. EF Core migrations and deployment (free tiers).
2. Web push reminders for when the app is closed.
3. Timed, no-AI skill checks every few weeks.
4. Job description import and a gap view.
5. Run code safely (a sandbox service) for objective test results.
