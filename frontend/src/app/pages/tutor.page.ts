import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorText } from '../core/auth.interceptor';
import { Skill } from '../core/models';

interface Turn { q: string; a: string; source: string }

@Component({
  selector: 'app-tutor',
  imports: [FormsModule],
  template: `
    <main class="page">
      <h1>Ask your tutor</h1>
      <p class="muted">Pick a topic and ask anything. The tutor explains from your level, uses small examples and asks you a question back. For an exercise, use the help buttons inside the task so your credit stays honest.</p>
      <section class="card stack">
        <div><label for="topic">Topic</label>
          <select id="topic" [(ngModel)]="topicId">
            @for (s of skills(); track s.key) { <optgroup [label]="s.name">@for (t of s.topics; track t.id) { <option [value]="t.id">{{ t.title }}</option> }</optgroup> }
          </select></div>
        <div><label for="q">Your question</label><textarea id="q" [(ngModel)]="question" maxlength="1500" placeholder="For example: when would I use a Dictionary instead of a List?"></textarea></div>
        @if (error()) { <div class="banner bad" role="alert">{{ error() }}</div> }
        <button class="btn primary" (click)="ask()" [disabled]="busy() || question.trim().length < 2">{{ busy() ? 'Thinking...' : 'Ask' }}</button>
      </section>
      @for (t of turns(); track $index) {
        <section class="card stack" aria-live="polite"><div class="wrap"><strong>You:</strong> {{ t.q }}</div><div class="answer"><strong>Tutor ({{ t.source }}):</strong>{{ '\n' }}{{ t.a }}</div></section>
      }
    </main>
  `
})
export class TutorPage implements OnInit {
  private api = inject(ApiService);
  skills = signal<Skill[]>([]); turns = signal<Turn[]>([]); error = signal(''); busy = signal(false);
  topicId = 'csharp.types'; question = '';
  async ngOnInit() { try { this.skills.set(await firstValueFrom(this.api.skills())); } catch (e) { this.error.set(errorText(e)); } }
  async ask() {
    this.busy.set(true); this.error.set('');
    try {
      const q = this.question; const r = await firstValueFrom(this.api.askTutor(q, this.topicId));
      this.turns.set([{ q, a: r.answer, source: r.source }, ...this.turns()]); this.question = '';
    } catch (e) { this.error.set(errorText(e)); }
    this.busy.set(false);
  }
}
