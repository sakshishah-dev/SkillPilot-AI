# SkillPilot AI

A personal AI tutor that plans each study day, teaches, checks what you can really do, and adjusts when life gets in the way.

It is built to be used daily for job preparation (full-stack plus applied AI), and to be a portfolio project in Angular and ASP.NET Core.

## What works today

| Area | Status |
| --- | --- |
| Account (email and password, JWT), invite-only registration | Done |
| Onboarding: goal, study minutes per weekday, confidence per skill, skill order | Done |
| Skill catalog: 10 skills, 54 topics (C#, DSA in the order you chose, OOP, SQL, JS, TS, Angular, ASP.NET, Python, GenAI) | Done |
| Weekly plan that never exceeds your time and never stacks a backlog | Done |
| Busy (20 min), Minimal (10 min) and Day off modes, with an audit trail of every plan change and its reason | Done |
| Daily tasks: Learn, Practice, Explain, Revision | Done |
| Evidence based mastery (watching is not knowing), spaced revision | Done |
| Practice exercises with progressive hints, tutor questions, solution only after an attempt | Done |
| Help level (independent, hint, teacher, solution) decided by the server and used to reduce credit | Done |
| Confidence kept separate from proven level, with the gap shown | Done |
| Study timer, minutes and "what I did" log, daily streak | Done |
| Reminders every 15 to 60 minutes while the timer runs (in the app or as a browser notification) | Done |
| Weekly review: planned vs actual, consistency, independent solves, revision list, recommendations | Done |
| "What do I need tomorrow" coach with places you need help, plus an optional AI note | Done |
| Claude and OpenAI behind one interface, offline demo tutor when no key is set | Done |
| Daily AI call limit per user | Done |

## Not built yet (honest list)

- Running your code. Reviews are done by the AI reading your code against a rubric. They can be wrong. Unverified (offline) reviews never earn more than "understood" credit.
- Push reminders while the app is closed (needs a server push step).
- Timed no-AI skill tests every few weeks.
- Importing job descriptions and a skill gap view.
- Database migrations. Tables are created on first start (EnsureCreated). Add EF migrations before the first schema change on real data.
- Hosting setup and CI.

## Run it

You need .NET 8 SDK and Node 22.

```bash
# 1. API (http://localhost:5000)
cd backend/src/SkillPilot.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5000
```

```bash
# 2. App (http://localhost:4200, proxies /api to the API)
cd frontend
npm install
npm start
```

Open http://localhost:4200, create an account and follow the setup. Without an AI key you get the offline demo tutor.

```bash
# Tests
cd backend && dotnet test
cd frontend && npx ng build
```

## Turning on the real AI tutor

Keys are read by the server only. Never put a key in the frontend.

```bash
cd backend/src/SkillPilot.Api
dotnet user-secrets init
dotnet user-secrets set "Ai:Anthropic:ApiKey" "<your key>"
dotnet user-secrets set "Ai:OpenAi:ApiKey" "<your key>"
```

In production use environment variables: `Ai__Anthropic__ApiKey`, `Ai__OpenAi__ApiKey`, `Jwt__Key` (32 or more random characters), `Registration__AllowedEmails__0` (your email), `Cors__Origins__0` (your app address). Models are set with `Ai__Anthropic__Model` and `Ai__OpenAi__Model`.

Use a PostgreSQL database for hosting: `Database__Provider=postgres` and `ConnectionStrings__Default=<connection string>`. Local development uses a SQLite file.

## How it works

See [docs/DESIGN.md](docs/DESIGN.md) for the mastery model, the planner rules, the AI design and the security choices.

## Using this project to actually learn

Building this app can steal the time you need to study. Suggested rule: at most 2 to 3 hours a week on SkillPilot, and write the next feature yourself first, then ask Claude to review it. The planner, mastery rules and tests in `backend/src/SkillPilot.Api/Core` are small and well covered, so they are a good place to read and change things.
