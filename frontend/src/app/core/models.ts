export interface TopicInfo {
  id: string; title: string; description: string; skillKey?: string; skillName?: string;
  language?: string; hasCode: boolean; resourceName?: string; resourceUrl?: string;
}
export type TaskKind = 'Learn' | 'Practice' | 'Explain' | 'Revision';
export type TaskStatus = 'Planned' | 'Done' | 'Skipped' | 'Missed';
export interface TaskItem {
  id: string; date: string; kind: TaskKind; status: TaskStatus; minutes: number; why: string; order: number; topic: TopicInfo;
}
export interface PlanChange { reason: string; summary: string; at: string }
export interface TodayPlan {
  date: string; mode: 'Normal' | 'Busy' | 'Minimal' | 'Off'; availableMinutes: number; studiedMinutes: number;
  plannedMinutes: number; doneMinutes: number; streak: number; movedRecently: number;
  lastChange: PlanChange | null; tasks: TaskItem[];
}
export interface WeekPlan { days: { date: string; availableMinutes: number; tasks: TaskItem[] }[] }
export interface Review {
  score: number; verdict: string; summary: string; issues: string[]; suggestions: string[];
  complexity: string; nextStep: string; verified: boolean; source: string;
}
export interface Exercise {
  id: string; topicId: string; prompt: string; starterCode: string; language: string;
  hintsTotal: number; hintsUsed: number; hints: string[]; attempts: number; solutionViewed: boolean;
  canViewSolution: boolean; teacherAsks: number; source: string; assist: 'Independent' | 'Hint' | 'Teacher' | 'Solution';
}
export interface TaskDetail {
  task: TaskItem; exercise: Exercise | null; explainPrompt: string | null; lastReview: Review | null; lastContent: string | null;
}
export interface SubmitResult { review: Review; assist: string; mastery: number; taskDone: boolean; exercise: Exercise; credit: string }
export interface SkillTopic { id: string; title: string; mastery: number; status: string; done: string[]; revisionDue: string | null }
export interface Skill {
  key: string; name: string; category: string; description: string; resourceName: string; resourceUrl: string;
  mastery: number; confidence: number; gap: number; topicsMastered: number; topicCount: number; topics: SkillTopic[];
}
export interface AiStatus { selected: string; active: string; anthropicConfigured: boolean; openaiConfigured: boolean; dailyLimit: number }
export interface Profile {
  targetRole: string; targetDate: string | null; availability: number[]; priority: string[]; aiProvider: string;
  timezone: string; reminderEnabled: boolean; reminderIntervalMinutes: number; onboardingDone: boolean;
  confidence: { skillKey: string; confidence: number }[]; ai: AiStatus;
}
export interface SessionRow { id: string; date: string; minutes: number; note: string; energy: number; taskId: string | null }
export interface SessionsResult { perDay: { date: string; minutes: number }[]; sessions: SessionRow[] }
export interface WeekReview {
  weekStart: string; weekEnd: string; availableMinutes: number; studiedMinutes: number; daysStudied: number; studyDays: number;
  tasksDone: number; tasksMoved: number; tasksSkipped: number; topicsMastered: string[]; independentSolves: number;
  assistedSolves: number; revisionNeeded: string[]; consistency: number;
  minutesByDay: { date: string; minutes: number; available: number }[]; recommendations: string[];
}
export interface Coach {
  today: { minutes: number; tasksDone: number; tasksTotal: number; notes: string[] };
  tomorrow: { date: string; tasks: TaskItem[] };
  help: { topic: TopicInfo; reason: string }[];
  summary: string; note: string | null; noteSource: string;
}
export interface AuthResult { token: string; user: { id: string; email: string; displayName: string } }
