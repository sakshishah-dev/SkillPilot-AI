import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorText } from '../core/auth.interceptor';
import { Coach, SessionsResult, WeekReview } from '../core/models';
import { fmtMinutes } from '../shared/util';

@Component({
  selector: 'app-review',
  imports: [DatePipe, RouterLink],
  template: `
    <main class="page">
      <h1>Review</h1>
      @if (error()) { <div class="banner bad" role="alert">{{ error() }}</div> }
      @if (!week() && !error()) { <div class="skeleton"></div><div class="skeleton"></div> }

      @if (coach(); as c) {
        <section class="card stack">
          <h2>Tomorrow</h2>
          <p class="small muted wrap">{{ c.summary }}</p>
          @for (t of c.tomorrow.tasks; track t.id) { <div class="row"><span class="chip">{{ t.kind }}</span><span class="grow wrap">{{ t.topic.title }}</span><span class="small muted">{{ fmt(t.minutes) }}</span></div> }
            @empty { <p class="small">Nothing planned for tomorrow yet.</p> }
          @if (c.help.length) {
            <div class="banner warn"><strong>Where you may need help</strong>
              @for (h of c.help; track h.topic.id) { <div class="small wrap">{{ h.topic.title }}: {{ h.reason }}</div> }
              <a routerLink="/tutor" class="small">Ask the tutor</a></div>
          }
          @if (c.note) { <div class="answer"><strong>Coach note ({{ c.noteSource }})</strong>{{ '\n' }}{{ c.note }}</div> }
          <button class="btn" (click)="coachNote()" [disabled]="busy()">{{ busy() ? 'Thinking...' : 'Write my coach note (uses 1 AI call)' }}</button>
        </section>
      }

      @if (week(); as w) {
        <section class="card stack">
          <h2>This week ({{ w.weekStart | date: 'd MMM' }} to {{ w.weekEnd | date: 'd MMM' }})</h2>
          <div class="row between"><span>Consistency</span><span class="score">{{ w.consistency }}<span class="small muted"> / 100</span></span></div>
          <p class="small muted">Counts days you studied at least 10 minutes (60 percent) and time against what you had available (40 percent). It rewards showing up, not marathon days.</p>
          <div class="row"><span class="chip primary">{{ fmt(w.studiedMinutes) }} studied</span><span class="chip">{{ w.daysStudied }} of {{ w.studyDays }} days</span>
            <span class="chip good">{{ w.tasksDone }} tasks done</span>@if (w.tasksMoved) { <span class="chip warn">{{ w.tasksMoved }} moved</span> }</div>
          @for (d of w.minutesByDay; track d.date) {
            <div class="row"><span class="small" style="width:56px">{{ d.date | date: 'EEE' }}</span>
              <div class="bar grow"><span [style.width.%]="d.available ? min(100, d.minutes * 100 / d.available) : (d.minutes ? 100 : 0)"></span></div>
              <span class="small muted" style="width:70px;text-align:right">{{ fmt(d.minutes) }}</span></div>
          }
          <h3>Real learning</h3>
          <p class="small">Solved alone: <strong>{{ w.independentSolves }}</strong>. Solved with help: <strong>{{ w.assistedSolves }}</strong>.</p>
          @if (w.topicsMastered.length) { <p class="small">Newly solid: {{ w.topicsMastered.join(', ') }}.</p> }
          @if (w.revisionNeeded.length) { <p class="small">Needs revision: {{ w.revisionNeeded.join(', ') }}.</p> }
          <h3>Next week</h3>
          <ul>@for (r of w.recommendations; track $index) { <li class="wrap">{{ r }}</li> }</ul>
        </section>
      }

      @if (log(); as l) {
        <h2>Your study log</h2>
        @for (s of l.sessions; track s.id) {
          <div class="card row between"><div class="grow wrap"><div class="small muted">{{ s.date | date: 'EEE d MMM' }}</div>{{ s.note || 'Study session' }}</div>
            <strong>{{ fmt(s.minutes) }}</strong><button class="btn small danger" (click)="remove(s.id)" aria-label="Delete this session">Delete</button></div>
        } @empty { <div class="card empty">No sessions yet. Start the timer on the Today page.</div> }
      }
    </main>
  `
})
export class ReviewPage implements OnInit {
  private api = inject(ApiService);
  week = signal<WeekReview | null>(null); coach = signal<Coach | null>(null); log = signal<SessionsResult | null>(null);
  error = signal(''); busy = signal(false); fmt = fmtMinutes; min = Math.min;

  async ngOnInit() { await this.load(); }
  async load() {
    try {
      const [w, c, l] = await Promise.all([firstValueFrom(this.api.weekReview()), firstValueFrom(this.api.coach(false)), firstValueFrom(this.api.sessions(14))]);
      this.week.set(w); this.coach.set(c); this.log.set(l);
    } catch (e) { this.error.set(errorText(e)); }
  }
  async coachNote() {
    this.busy.set(true);
    try { this.coach.set(await firstValueFrom(this.api.coach(true))); } catch (e) { this.error.set(errorText(e)); }
    this.busy.set(false);
  }
  async remove(id: string) {
    try { await firstValueFrom(this.api.deleteSession(id)); await this.load(); } catch (e) { this.error.set(errorText(e)); }
  }
}
