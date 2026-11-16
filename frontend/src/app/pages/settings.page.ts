import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { errorText } from '../core/auth.interceptor';
import { Profile, Skill } from '../core/models';
import { DAY_NAMES, fmtMinutes } from '../shared/util';

@Component({
  selector: 'app-settings',
  imports: [FormsModule],
  template: `
    <main class="page">
      <h1>Settings</h1>
      @if (error()) { <div class="banner bad" role="alert">{{ error() }}</div> }
      @if (saved()) { <div class="banner good" role="status">Saved. Your plan was rebuilt from your real progress.</div> }
      @if (!profile()) { <div class="skeleton"></div><div class="skeleton"></div> }
      @else {
        <section class="card stack">
          <h2>Goal</h2>
          <div><label for="role">Target role</label><input id="role" type="text" [(ngModel)]="role" maxlength="120"></div>
          <div><label for="td">Target date</label><input id="td" type="date" [(ngModel)]="targetDate"></div>
        </section>

        <section class="card stack">
          <h2>Study time</h2>
          <p class="small muted">The plan never goes above this. On a tough day use Busy or Minimal mode on the Today page instead of changing it here.</p>
          @for (d of days; track d; let i = $index) {
            <div class="row"><span style="width:100px">{{ d }}</span>
              <input class="grow" type="range" min="0" max="300" step="15" [ngModel]="avail[i]" (ngModelChange)="avail[i] = $event" [attr.aria-label]="d + ' minutes'">
              <strong style="width:70px;text-align:right">{{ fmt(avail[i]) }}</strong></div>
          }
        </section>

        <section class="card stack">
          <h2>What to learn first</h2>
          @for (k of order(); track k; let i = $index) {
            <div class="row"><span class="chip">{{ i + 1 }}</span><span class="grow">{{ name(k) }}</span>
              <button class="btn small" (click)="move(i, -1)" [disabled]="i === 0" [attr.aria-label]="'Move ' + name(k) + ' up'">Up</button>
              <button class="btn small" (click)="move(i, 1)" [disabled]="i === order().length - 1" [attr.aria-label]="'Move ' + name(k) + ' down'">Down</button></div>
          }
        </section>

        <section class="card stack">
          <h2>AI tutor</h2>
          <p class="small muted">Keys live only on the server. You cannot paste them here, which keeps them safe. Currently active: <strong>{{ profile()!.ai.active }}</strong>.
            Claude {{ profile()!.ai.anthropicConfigured ? 'is set up' : 'is not set up' }}, OpenAI {{ profile()!.ai.openaiConfigured ? 'is set up' : 'is not set up' }}. Limit: {{ profile()!.ai.dailyLimit }} AI calls a day.</p>
          <div><label for="prov">Provider</label>
            <select id="prov" [(ngModel)]="provider">
              <option value="auto">Automatic (Claude first, then OpenAI)</option><option value="anthropic">Claude</option>
              <option value="openai">OpenAI</option><option value="offline">Offline demo tutor (no AI)</option>
            </select></div>
        </section>

        <section class="card stack">
          <h2>Study reminders</h2>
          <label class="row" style="font-weight:500"><input type="checkbox" [(ngModel)]="remind"> Remind me during a study session</label>
          <div><label for="int">Every</label>
            <select id="int" [(ngModel)]="interval" [disabled]="!remind"><option [ngValue]="15">15 minutes</option><option [ngValue]="30">30 minutes</option><option [ngValue]="45">45 minutes</option><option [ngValue]="60">60 minutes</option></select></div>
          <p class="small muted">Reminders appear while the app is open with the timer running. Allow notifications for a pop-up on your device. Reminders while the app is closed need a server push step that is planned for later.</p>
          <button class="btn" (click)="allowNotifications()">Allow browser notifications</button>
          @if (notifyMsg()) { <p class="small">{{ notifyMsg() }}</p> }
        </section>

        <div class="row"><button class="btn primary" (click)="save()" [disabled]="busy()">{{ busy() ? 'Saving...' : 'Save settings' }}</button>
          <button class="btn danger" (click)="auth.logout()">Sign out</button></div>
      }
    </main>
  `
})
export class SettingsPage implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  profile = signal<Profile | null>(null); skills = signal<Skill[]>([]); order = signal<string[]>([]);
  error = signal(''); saved = signal(false); busy = signal(false); notifyMsg = signal('');
  days = DAY_NAMES; fmt = fmtMinutes;
  role = ''; targetDate = ''; avail: number[] = []; provider = 'auto'; remind = true; interval = 30;

  async ngOnInit() {
    try {
      const [p, s] = await Promise.all([firstValueFrom(this.api.profile()), firstValueFrom(this.api.skills())]);
      this.skills.set(s); this.apply(p);
    } catch (e) { this.error.set(errorText(e)); }
  }
  private apply(p: Profile) {
    this.profile.set(p); this.role = p.targetRole; this.targetDate = p.targetDate ?? ''; this.avail = [...p.availability];
    this.order.set([...p.priority]); this.provider = p.aiProvider; this.remind = p.reminderEnabled; this.interval = p.reminderIntervalMinutes || 30;
  }
  name(k: string) { return this.skills().find(s => s.key === k)?.name ?? k; }
  move(i: number, d: number) {
    const o = [...this.order()]; const j = i + d; if (j < 0 || j >= o.length) return;
    [o[i], o[j]] = [o[j], o[i]]; this.order.set(o);
  }
  async save() {
    this.busy.set(true); this.error.set(''); this.saved.set(false);
    try {
      const p = await firstValueFrom(this.api.saveProfile({
        targetRole: this.role, targetDate: this.targetDate || null, availability: this.avail, priority: this.order(),
        aiProvider: this.provider, reminderEnabled: this.remind, reminderIntervalMinutes: this.interval
      }));
      this.apply(p); this.saved.set(true);
    } catch (e) { this.error.set(errorText(e)); }
    this.busy.set(false);
  }
  async allowNotifications() {
    if (!('Notification' in window)) { this.notifyMsg.set('This browser does not support notifications.'); return; }
    try { this.notifyMsg.set(`Notifications: ${await Notification.requestPermission()}`); }
    catch { this.notifyMsg.set('Could not ask for permission in this browser.'); }
  }
}
