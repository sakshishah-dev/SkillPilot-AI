import { Injectable, inject, signal } from '@angular/core';
import { ApiService } from './api.service';

const KEY = 'sp_timer';

interface Saved { startedAt: number; taskId: string | null; interval: number; lastPing: number }

function load(): Saved | null {
  try { const r = localStorage.getItem(KEY); return r ? JSON.parse(r) : null; } catch { return null; }
}
function save(s: Saved | null) {
  try { s ? localStorage.setItem(KEY, JSON.stringify(s)) : localStorage.removeItem(KEY); } catch { /* ignore */ }
}

/**
 * A study timer that survives page reloads. Reminders appear in the app while it is open,
 * and as a browser notification if allowed. Phone push while the app is closed is a later step.
 */
@Injectable({ providedIn: 'root' })
export class TimerService {
  private api = inject(ApiService);
  private handle: ReturnType<typeof setInterval> | null = null;

  readonly running = signal(false);
  readonly seconds = signal(0);
  readonly nudge = signal<string | null>(null);
  taskId: string | null = null;
  private interval = 30;
  private startedAt = 0;
  private lastPing = 0;

  constructor() {
    const s = load();
    if (s) { this.startedAt = s.startedAt; this.taskId = s.taskId; this.interval = s.interval; this.lastPing = s.lastPing; this.run(); }
  }

  start(intervalMinutes: number, taskId: string | null = null) {
    if (this.running()) return;
    this.startedAt = Date.now(); this.lastPing = this.startedAt; this.taskId = taskId; this.interval = intervalMinutes;
    save({ startedAt: this.startedAt, taskId, interval: intervalMinutes, lastPing: this.lastPing });
    this.run();
  }

  private run() {
    this.running.set(true);
    this.tick();
    this.handle = setInterval(() => this.tick(), 1000);
  }

  private tick() {
    this.seconds.set(Math.floor((Date.now() - this.startedAt) / 1000));
    if (this.interval > 0 && Date.now() - this.lastPing >= this.interval * 60000) {
      this.lastPing = Date.now();
      const mins = Math.round((Date.now() - this.startedAt) / 60000);
      const msg = `${mins} minutes of study so far. Stretch, drink water, then keep going or stop when you are ready.`;
      this.nudge.set(msg);
      try {
        if ('Notification' in window && Notification.permission === 'granted') new Notification('SkillPilot', { body: msg });
      } catch { /* some browsers block constructor use */ }
      save({ startedAt: this.startedAt, taskId: this.taskId, interval: this.interval, lastPing: this.lastPing });
    }
  }

  dismissNudge() { this.nudge.set(null); }

  /** Stops the timer and saves the time as a session. Returns the minutes logged (0 if under a minute). */
  async stop(note: string, energy: number): Promise<number> {
    const minutes = Math.round((Date.now() - this.startedAt) / 60000);
    if (this.handle) clearInterval(this.handle);
    this.handle = null; this.running.set(false); this.seconds.set(0); this.nudge.set(null);
    const taskId = this.taskId;
    this.taskId = null; save(null);
    if (minutes < 1) return 0;
    await new Promise<void>((resolve, reject) =>
      this.api.logSession(Math.min(minutes, 600), note, energy, taskId).subscribe({ next: () => resolve(), error: reject }));
    return minutes;
  }

  discard() {
    if (this.handle) clearInterval(this.handle);
    this.handle = null; this.running.set(false); this.seconds.set(0); this.nudge.set(null); this.taskId = null; save(null);
  }
}
