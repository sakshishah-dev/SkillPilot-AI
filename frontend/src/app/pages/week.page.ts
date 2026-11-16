import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorText } from '../core/auth.interceptor';
import { PlanChange, WeekPlan } from '../core/models';
import { KIND_LABEL, fmtMinutes } from '../shared/util';

@Component({
  selector: 'app-week',
  imports: [DatePipe, RouterLink],
  template: `
    <main class="page">
      <div class="row between"><h1>Your next 7 days</h1><button class="btn small" (click)="rebuild()" [disabled]="busy()">Rebuild plan</button></div>
      <p class="muted">The plan never goes over the time you set. If you miss a day, nothing piles up: each topic simply continues from its next step.</p>
      @if (error()) { <div class="banner bad" role="alert">{{ error() }}</div> }
      @if (!week() && !error()) { <div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div> }
      @for (d of week()?.days; track d.date) {
        <section class="card">
          <div class="row between"><strong>{{ d.date | date: 'EEEE, d MMM' }}</strong><span class="small muted">{{ fmt(d.availableMinutes) }} available</span></div>
          @for (t of d.tasks; track t.id) {
            <a class="row" style="padding:8px 0;border-top:1px solid var(--line);text-decoration:none;color:inherit;margin-top:6px" [routerLink]="['/task', t.id]">
              <span class="chip" [class.good]="t.status === 'Done'">{{ label[t.kind] }}</span>
              <span class="grow wrap" [style.text-decoration]="t.status === 'Done' ? 'line-through' : 'none'">{{ t.topic.title }}</span>
              <span class="small muted">{{ fmt(t.minutes) }}</span>
            </a>
          } @empty { <p class="small muted" style="margin:6px 0 0">{{ d.availableMinutes === 0 ? 'Rest day.' : 'Nothing planned.' }}</p> }
        </section>
      }
      <h2 style="margin-top:20px">Why the plan changed</h2>
      <p class="small muted">Every automatic change is listed with its reason.</p>
      @for (c of changes(); track c.at) {
        <div class="card"><div class="small muted">{{ c.at | date: 'd MMM, HH:mm' }}</div><p class="wrap" style="margin:4px 0">{{ c.reason }}</p><p class="small muted" style="margin:0">{{ c.summary }}</p></div>
      } @empty { <div class="card empty">No changes yet.</div> }
    </main>
  `
})
export class WeekPage implements OnInit {
  private api = inject(ApiService);
  week = signal<WeekPlan | null>(null); changes = signal<PlanChange[]>([]);
  error = signal(''); busy = signal(false); fmt = fmtMinutes; label = KIND_LABEL;

  ngOnInit() { this.load(); }
  async load() {
    try {
      const [w, c] = await Promise.all([firstValueFrom(this.api.week()), firstValueFrom(this.api.changes())]);
      this.week.set(w); this.changes.set(c);
    } catch (e) { this.error.set(errorText(e)); }
  }
  async rebuild() {
    this.busy.set(true);
    try { await firstValueFrom(this.api.replan()); await this.load(); } catch (e) { this.error.set(errorText(e)); }
    this.busy.set(false);
  }
}
