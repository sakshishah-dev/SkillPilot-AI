import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorText } from '../core/auth.interceptor';
import { Skill } from '../core/models';
import { DAY_NAMES, fmtMinutes } from '../shared/util';

@Component({
  selector: 'app-onboarding',
  imports: [FormsModule],
  template: `
    <main class="page">
      <h1>Let's set you up</h1>
      <p class="muted">Three quick steps. You can change everything later in Settings.</p>

      @if (loading()) { <div class="skeleton"></div><div class="skeleton"></div> }
      @else {
        @if (step() === 1) {
          <section class="card stack">
            <h2>1. Your goal and your time</h2>
            <div><label for="role">What are you preparing for?</label><input id="role" type="text" [(ngModel)]="role" maxlength="120"></div>
            <div><label for="td">Target date to switch (optional)</label><input id="td" type="date" [(ngModel)]="targetDate"></div>
            <p class="small muted">How many minutes can you study on a normal day? Be realistic. The plan never goes above this.</p>
            @for (d of days; track d; let i = $index) {
              <div class="row"><span style="width:100px">{{ d }}</span>
                <input class="grow" type="range" min="0" max="300" step="15" [ngModel]="avail[i]" (ngModelChange)="avail[i] = $event" [attr.aria-label]="d + ' minutes'">
                <strong style="width:70px;text-align:right">{{ fmt(avail[i]) }}</strong></div>
            }
            <p class="small">About <strong>{{ fmt(total()) }}</strong> a week.</p>
            <button class="btn primary" (click)="step.set(2)">Next</button>
          </section>
        }
        @if (step() === 2) {
          <section class="card stack">
            <h2>2. How confident do you feel?</h2>
            <p class="small muted">Just a feeling, 0 to 10. It is kept apart from your proven level, and the app will show you any gap between the two. Skip what you have not touched.</p>
            @for (s of skills(); track s.key) {
              <div><div class="row between"><label [attr.for]="'c-' + s.key">{{ s.name }}</label><strong>{{ conf[s.key] }}/10</strong></div>
                <input [id]="'c-' + s.key" type="range" min="0" max="10" [ngModel]="conf[s.key]" (ngModelChange)="conf[s.key] = $event"></div>
            }
            <div class="row"><button class="btn" (click)="step.set(1)">Back</button><button class="btn primary" (click)="step.set(3)">Next</button></div>
          </section>
        }
        @if (step() === 3) {
          <section class="card stack">
            <h2>3. What comes first?</h2>
            <p class="small muted">Your main skill gets about 60 percent of study time and DSA about 40 percent. Put the skill you want to learn first at the top.</p>
            @for (k of order(); track k; let i = $index) {
              <div class="row"><span class="chip">{{ i + 1 }}</span><span class="grow">{{ name(k) }}</span>
                <button class="btn small" (click)="move(i, -1)" [disabled]="i === 0" [attr.aria-label]="'Move ' + name(k) + ' up'">Up</button>
                <button class="btn small" (click)="move(i, 1)" [disabled]="i === order().length - 1" [attr.aria-label]="'Move ' + name(k) + ' down'">Down</button></div>
            }
            @if (error()) { <div class="banner bad" role="alert">{{ error() }}</div> }
            <div class="row"><button class="btn" (click)="step.set(2)">Back</button>
              <button class="btn primary" (click)="finish()" [disabled]="saving()">{{ saving() ? 'Building your plan...' : 'Build my plan' }}</button></div>
          </section>
        }
      }
    </main>
  `
})
export class OnboardingPage implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);
  days = DAY_NAMES; fmt = fmtMinutes;
  step = signal(1); loading = signal(true); saving = signal(false); error = signal('');
  skills = signal<Skill[]>([]); order = signal<string[]>([]);
  role = ''; targetDate = ''; avail = [90, 90, 90, 90, 90, 180, 180]; conf: Record<string, number> = {};

  async ngOnInit() {
    try {
      const [p, s] = await Promise.all([firstValueFrom(this.api.profile()), firstValueFrom(this.api.skills())]);
      this.skills.set(s ?? []);
      this.role = p?.targetRole ?? ''; this.targetDate = p?.targetDate ?? ''; this.avail = [...(p?.availability ?? this.avail)];
      this.order.set([...(p?.priority ?? [])]);
      for (const sk of s ?? []) this.conf[sk.key] = p?.confidence.find(c => c.skillKey === sk.key)?.confidence ?? 0;
    } catch (e) { this.error.set(errorText(e)); }
    this.loading.set(false);
  }

  total() { return this.avail.reduce((a, b) => a + b, 0); }
  name(k: string) { return this.skills().find(s => s.key === k)?.name ?? k; }
  move(i: number, d: number) {
    const o = [...this.order()]; const j = i + d;
    if (j < 0 || j >= o.length) return;
    [o[i], o[j]] = [o[j], o[i]]; this.order.set(o);
  }

  async finish() {
    this.saving.set(true); this.error.set('');
    try {
      await firstValueFrom(this.api.saveConfidence(Object.entries(this.conf).map(([skillKey, confidence]) => ({ skillKey, confidence }))));
      await firstValueFrom(this.api.saveProfile({
        targetRole: this.role || undefined, targetDate: this.targetDate || null,
        availability: this.avail, priority: this.order(), onboardingDone: true
      }));
      await this.router.navigateByUrl('/today');
    } catch (e) { this.error.set(errorText(e)); }
    this.saving.set(false);
  }
}
