import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { TimerService } from '../core/timer.service';
import { errorText } from '../core/auth.interceptor';
import { Profile, SessionRow, TodayPlan } from '../core/models';
import { KIND_LABEL, KIND_LETTER, fmtMinutes, secondsToClock } from '../shared/util';

@Component({
  selector: 'app-today',
  imports: [RouterLink, FormsModule, DatePipe],
  template: `
    <main class="page">
      <div class="row between">
        <div><h1>Hello, {{ auth.user()?.displayName }}</h1>
          @if (plan(); as p) { <p class="muted">{{ p.date | date: 'EEEE, d MMMM' }}</p> }</div>
        @if (plan(); as p) { <span class="chip primary" title="Days in a row with at least 10 minutes">Streak {{ p.streak }}</span> }
      </div>

      @if (error()) { <div class="banner bad" role="alert">{{ error() }} <button class="btn small" (click)="load()">Retry</button></div> }

      @if (!plan() && !error()) { <div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div> }
      @if (plan(); as p) {
        @if (needsSetup()) {
          <div class="banner warn">You have not set your study time yet. <a routerLink="/welcome">Finish setup</a></div>
        }
        @if (p.lastChange && !hideChange()) {
          <div class="banner">
            <div class="row between"><strong>Why your plan changed</strong><button class="btn small" (click)="hideChange.set(true)">Hide</button></div>
            <p class="small wrap" style="margin:6px 0 0">{{ p.lastChange.reason }} {{ p.lastChange.summary }}</p>
          </div>
        }
        @if (p.movedRecently > 0) {
          <div class="banner good small">{{ p.movedRecently }} task(s) from earlier days moved on. That is fine. Everything continues from where you stopped.</div>
        }

        <section class="card">
          <div class="row between"><strong>Today's time</strong><span class="small muted">{{ fmt(p.studiedMinutes) }} studied of {{ fmt(p.availableMinutes) }}</span></div>
          <div class="bar" style="margin:8px 0 12px" role="progressbar" [attr.aria-valuenow]="pct()" aria-valuemin="0" aria-valuemax="100"><span [style.width.%]="pct()"></span></div>
          <div class="seg" role="group" aria-label="How is today">
            @for (m of modes; track m.v) { <button [class.on]="p.mode === m.v" (click)="setMode(m.v)" [disabled]="busy()">{{ m.l }}</button> }
          </div>
          <p class="small muted" style="margin:8px 0 0">Busy day: 20 minutes. Minimal: 10 minutes. A short day still counts and tomorrow is not made heavier.</p>
        </section>

        <section class="card">
          @if (timer.running()) {
            <div class="row between"><div><div class="muted small">Study timer</div><div class="timer">{{ clock(timer.seconds()) }}</div></div>
              <button class="btn primary" (click)="finishing.set(true)">Stop and save</button></div>
            @if (finishing()) {
              <div class="stack" style="margin-top:12px">
                <div><label for="what">What did you do?</label><textarea id="what" [(ngModel)]="note" maxlength="1000" placeholder="For example: read about Dictionary, solved two problems"></textarea></div>
                <div><label for="en">Energy</label><select id="en" [(ngModel)]="energy"><option [ngValue]="1">Low</option><option [ngValue]="2">Normal</option><option [ngValue]="3">High</option></select></div>
                <div class="row"><button class="btn primary" (click)="stopTimer()" [disabled]="busy()">Save session</button>
                  <button class="btn" (click)="finishing.set(false)">Keep going</button>
                  <button class="btn danger" (click)="discard()">Discard</button></div>
              </div>
            }
          } @else {
            <div class="row between"><div><strong>Start a study session</strong><div class="muted small">The timer saves your minutes and what you did.</div></div>
              <button class="btn primary" (click)="startTimer()">Start timer</button></div>
          }
        </section>

        <h2>Today's tasks</h2>
        @for (t of p.tasks; track t.id) {
          <a class="card task" [class.done]="t.status === 'Done'" [routerLink]="['/task', t.id]">
            <span class="dot" aria-hidden="true">{{ letter[t.kind] }}</span>
            <span class="grow"><span class="title wrap" style="font-weight:700">{{ label[t.kind] }}: {{ t.topic.title }}</span><br>
              <span class="small muted">{{ t.topic.skillName }} - {{ fmt(t.minutes) }}</span></span>
            @if (t.status === 'Done') { <span class="chip good">Done</span> }
            @else if (t.status === 'Skipped') { <span class="chip">Skipped</span> }
            @else if (t.status === 'Missed') { <span class="chip warn">Moved</span> }
          </a>
        } @empty {
          <div class="card empty">
            @if (p.mode === 'Off') { Day off. Rest well, your plan continues tomorrow. }
            @else if (p.availableMinutes === 0) { No study time is set for today. }
            @else { Nothing left for today. You have used your time, or everything is done. }
          </div>
        }

        <section class="card stack">
          <strong>Log time without the timer</strong>
          <div class="row"><input style="width:110px" type="number" min="1" max="600" [(ngModel)]="manualMinutes" aria-label="Minutes"><span class="muted">minutes</span></div>
          <input type="text" [(ngModel)]="manualNote" maxlength="1000" placeholder="What did you do?" aria-label="What did you do">
          <button class="btn" (click)="logManual()" [disabled]="busy() || !manualMinutes">Save</button>
        </section>

        @if (todaySessions().length) {
          <h2>Logged today</h2>
          @for (s of todaySessions(); track s.id) {
            <div class="card row between"><span class="wrap grow">{{ s.note || 'Study session' }}</span><strong>{{ fmt(s.minutes) }}</strong></div>
          }
        }
      }
    </main>
  `
})
export class TodayPage implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  timer = inject(TimerService);

  plan = signal<TodayPlan | null>(null);
  profile = signal<Profile | null>(null);
  sessions = signal<SessionRow[]>([]);
  error = signal(''); busy = signal(false); hideChange = signal(false); finishing = signal(false);
  note = ''; energy = 2; manualMinutes: number | null = null; manualNote = '';
  fmt = fmtMinutes; clock = secondsToClock; label = KIND_LABEL; letter = KIND_LETTER;
  modes = [{ v: 'Normal', l: 'Normal' }, { v: 'Busy', l: 'Busy' }, { v: 'Minimal', l: 'Minimal' }, { v: 'Off', l: 'Day off' }];

  pct = computed(() => {
    const p = this.plan();
    return p && p.availableMinutes > 0 ? Math.min(100, Math.round((p.studiedMinutes / p.availableMinutes) * 100)) : 0;
  });
  needsSetup = computed(() => this.profile() ? !this.profile()!.onboardingDone : false);
  todaySessions = computed(() => { const d = this.plan()?.date; return this.sessions().filter(s => s.date === d); });

  ngOnInit() { this.load(); }

  async load() {
    this.error.set('');
    try {
      const [p, prof, s] = await Promise.all([firstValueFrom(this.api.today()), firstValueFrom(this.api.profile()), firstValueFrom(this.api.sessions(2))]);
      this.plan.set(p); this.profile.set(prof); this.sessions.set(s.sessions);
    } catch (e) { this.error.set(errorText(e)); }
  }

  async setMode(mode: string) {
    this.busy.set(true);
    try { this.plan.set(await firstValueFrom(this.api.setDayMode(mode))); this.hideChange.set(false); }
    catch (e) { this.error.set(errorText(e)); }
    this.busy.set(false);
  }

  startTimer() {
    const prof = this.profile();
    if (prof?.reminderEnabled && 'Notification' in window && Notification.permission === 'default') {
      try { Notification.requestPermission(); } catch { /* ignore */ }
    }
    this.timer.start(prof?.reminderEnabled ? prof.reminderIntervalMinutes : 0);
  }

  async stopTimer() {
    this.busy.set(true);
    try { await this.timer.stop(this.note, this.energy); this.note = ''; this.finishing.set(false); await this.load(); }
    catch (e) { this.error.set(errorText(e)); }
    this.busy.set(false);
  }

  discard() { this.timer.discard(); this.finishing.set(false); }

  async logManual() {
    if (!this.manualMinutes) return;
    this.busy.set(true);
    try {
      await firstValueFrom(this.api.logSession(this.manualMinutes, this.manualNote, 2));
      this.manualMinutes = null; this.manualNote = ''; await this.load();
    } catch (e) { this.error.set(errorText(e)); }
    this.busy.set(false);
  }
}
