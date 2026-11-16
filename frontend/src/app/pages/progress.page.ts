import { Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorText } from '../core/auth.interceptor';
import { Skill } from '../core/models';
import { masteryLabel } from '../shared/util';

@Component({
  selector: 'app-progress',
  template: `
    <main class="page">
      <h1>Your skills</h1>
      <p class="muted">The bar is what you have <strong>proven</strong> by doing. The orange mark is how confident you <em>feel</em>. Watching a lesson alone never lifts the bar past 25.</p>
      @if (error()) { <div class="banner bad" role="alert">{{ error() }}</div> }
      @if (!skills() && !error()) { <div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div> }
      @for (s of skills(); track s.key) {
        <section class="card">
          <button class="btn" style="width:100%;justify-content:space-between;text-align:left" (click)="toggle(s.key)" [attr.aria-expanded]="open() === s.key">
            <span class="wrap">{{ s.name }}</span><span class="small muted">{{ s.mastery }}/100 - {{ s.topicsMastered }}/{{ s.topicCount }} topics</span>
          </button>
          <div class="bar" style="margin-top:10px" role="img" [attr.aria-label]="s.name + ' proven level ' + s.mastery + ' out of 100'">
            <span [class.good]="s.mastery >= 70" [style.width.%]="s.mastery"></span>
            @if (s.confidence > 0) { <i class="marker" [style.left.%]="s.confidence * 10"></i> }
          </div>
          @if (s.gap >= 30 && s.confidence > 0) { <p class="small" style="margin:8px 0 0;color:var(--warn)">You feel more sure than your evidence shows. An independent exercise will tell the truth.</p> }
          @if (open() === s.key) {
            <div style="margin-top:10px">
              <p class="small muted">{{ s.description }} <a [href]="s.resourceUrl" target="_blank" rel="noopener noreferrer">{{ s.resourceName }}</a></p>
              @for (t of s.topics; track t.id) {
                <div class="row" style="padding:6px 0;border-top:1px solid var(--line)">
                  <span class="grow wrap">{{ t.title }}</span>
                  <span class="chip" [class.good]="t.status === 'mastered'" [class.primary]="t.status === 'learning'">{{ text(t.mastery) }}</span>
                  <strong class="small" style="width:34px;text-align:right">{{ t.mastery }}</strong>
                </div>
              }
            </div>
          }
        </section>
      }
    </main>
  `
})
export class ProgressPage implements OnInit {
  private api = inject(ApiService);
  skills = signal<Skill[] | null>(null); open = signal(''); error = signal(''); text = masteryLabel;
  async ngOnInit() {
    try { this.skills.set(await firstValueFrom(this.api.skills())); } catch (e) { this.error.set(errorText(e)); }
  }
  toggle(k: string) { this.open.set(this.open() === k ? '' : k); }
}
