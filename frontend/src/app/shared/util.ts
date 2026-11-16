import { TaskKind } from '../core/models';

export function fmtMinutes(m: number): string {
  if (m < 60) return `${m} min`;
  const h = Math.floor(m / 60), r = m % 60;
  return r ? `${h} h ${r} min` : `${h} h`;
}

export const KIND_LABEL: Record<TaskKind, string> = { Learn: 'Learn', Practice: 'Practice', Explain: 'Explain', Revision: 'Revise' };
export const KIND_LETTER: Record<TaskKind, string> = { Learn: 'L', Practice: 'P', Explain: 'E', Revision: 'R' };
export const DAY_NAMES = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

export function masteryLabel(m: number): string {
  return m >= 70 ? 'Solid' : m >= 40 ? 'Getting there' : m > 0 ? 'Just started' : 'Not started';
}
export function secondsToClock(s: number): string {
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), sec = s % 60;
  const p = (n: number) => String(n).padStart(2, '0');
  return h ? `${h}:${p(m)}:${p(sec)}` : `${p(m)}:${p(sec)}`;
}

const LANG_NAMES: Record<string, string> = { csharp: 'C#', javascript: 'JavaScript', typescript: 'TypeScript', python: 'Python', sql: 'SQL' };
export function langName(l: string): string { return LANG_NAMES[l] ?? l; }
