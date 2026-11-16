import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API_BASE } from './config';
import {
  Coach, Exercise, Profile, Review, SessionsResult, Skill, SubmitResult, TaskDetail, TodayPlan, WeekPlan, WeekReview, PlanChange
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private u = (p: string) => `${API_BASE}${p}`;

  today() { return this.http.get<TodayPlan>(this.u('/plan/today')); }
  week() { return this.http.get<WeekPlan>(this.u('/plan/week')); }
  changes() { return this.http.get<PlanChange[]>(this.u('/plan/changes')); }
  setDayMode(mode: string) { return this.http.post<TodayPlan>(this.u('/plan/day-mode'), { mode }); }
  replan() { return this.http.post<TodayPlan>(this.u('/plan/replan'), {}); }

  task(id: string) { return this.http.get<TaskDetail>(this.u(`/tasks/${id}`)); }
  completeTask(id: string, minutes: number, note?: string) { return this.http.post(this.u(`/tasks/${id}/complete`), { minutes, note }); }
  skipTask(id: string) { return this.http.post(this.u(`/tasks/${id}/skip`), {}); }
  startExercise(taskId: string) { return this.http.post<Exercise>(this.u(`/tasks/${taskId}/exercise`), {}); }
  explain(taskId: string, text: string) {
    return this.http.post<{ review: Review; taskDone: boolean; mastery: number }>(this.u(`/tasks/${taskId}/explain`), { text });
  }

  hint(id: string) { return this.http.post<Exercise>(this.u(`/exercises/${id}/hint`), {}); }
  solution(id: string) { return this.http.post<{ exercise: Exercise; solution: string }>(this.u(`/exercises/${id}/solution`), {}); }
  askExercise(id: string, question: string) {
    return this.http.post<{ answer: string; source: string; exercise: Exercise }>(this.u(`/exercises/${id}/ask`), { question });
  }
  submit(id: string, code: string) { return this.http.post<SubmitResult>(this.u(`/exercises/${id}/submit`), { code }); }
  askTutor(question: string, topicId?: string) {
    return this.http.post<{ answer: string; source: string }>(this.u('/tutor/ask'), { question, topicId });
  }

  skills() { return this.http.get<Skill[]>(this.u('/skills')); }
  profile() { return this.http.get<Profile>(this.u('/profile')); }
  saveProfile(body: Partial<{
    targetRole: string; targetDate: string | null; availability: number[]; priority: string[]; aiProvider: string;
    timezone: string; reminderEnabled: boolean; reminderIntervalMinutes: number; onboardingDone: boolean;
  }>) { return this.http.put<Profile>(this.u('/profile'), body); }
  saveConfidence(items: { skillKey: string; confidence: number }[]) { return this.http.put<Profile>(this.u('/profile/confidence'), items); }

  logSession(minutes: number, note: string, energy: number, taskId?: string | null, date?: string) {
    return this.http.post(this.u('/sessions'), { minutes, note, energy, taskId, date });
  }
  sessions(days = 14) { return this.http.get<SessionsResult>(this.u(`/sessions?days=${days}`)); }
  deleteSession(id: string) { return this.http.delete(this.u(`/sessions/${id}`)); }

  weekReview() { return this.http.get<WeekReview>(this.u('/review/week')); }
  coach(ai: boolean) { return this.http.get<Coach>(this.u(`/coach/tomorrow?ai=${ai}`)); }
}
