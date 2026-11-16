import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../core/api.service';
import { errorText } from '../core/auth.interceptor';
import { Exercise, Review, SubmitResult, TaskDetail } from '../core/models';
import { KIND_LABEL, fmtMinutes, langName, masteryLabel } from '../shared/util';

@Component({
  selector: 'app-task',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="page">
      <a routerLink="/today" class="small">Back to today</a>
      @if (error()) { <div class="banner bad" role="alert">{{ error() }}</div> }
      @if (!detail() && !error()) { <div class="skeleton"></div><div class="skeleton"></div> }
      @if (detail(); as d) {
        <div class="row" style="margin-top:8px">
          <span class="chip primary">{{ label[d.task.kind] }}</span><span class="chip">{{ fmt(d.task.minutes) }}</span>
          @if (d.task.status === 'Done') { <span class="chip good">Done</span> }
        </div>
        <h1 class="wrap">{{ d.task.topic.title }}</h1>
        <p class="muted wrap">{{ d.task.topic.description }}</p>
        <div class="banner small"><strong>Why this today:</strong> {{ d.task.why }}</div>

        <!-- LEARN -->
        @if (d.task.kind === 'Learn') {
          <section class="card stack">
            <h2>Read, then check yourself</h2>
            <p>Use this resource for the lesson. Read actively: try each small example yourself instead of only reading.</p>
            @if (d.task.topic.resourceUrl) {
              <a class="btn" [href]="d.task.topic.resourceUrl" target="_blank" rel="noopener noreferrer">Open: {{ d.task.topic.resourceName }}</a>
            }
            <div class="row"><label for="mins" style="margin:0">Time spent</label><input id="mins" style="width:100px" type="number" min="0" max="600" [(ngModel)]="minutes"><span class="muted">minutes</span></div>
            <div class="row">
              <button class="btn primary" (click)="complete()" [disabled]="busy() || d.task.status === 'Done'">Mark lesson done</button>
              <button class="btn" (click)="skip()" [disabled]="busy() || d.task.status === 'Done'">Skip for now</button>
            </div>
            <p class="small muted">Reading counts as the lowest level of proof. The exercise and explanation steps are what raise your real level.</p>
          </section>
        }

        <!-- EXPLAIN -->
        @if (d.task.kind === 'Explain') {
          <section class="card stack">
            <h2>Explain it back</h2>
            <p class="wrap">{{ d.explainPrompt }}</p>
            <textarea [(ngModel)]="explanation" maxlength="6000" aria-label="Your explanation" placeholder="Write it in your own words. No copying."></textarea>
            <button class="btn primary" (click)="submitExplain()" [disabled]="busy() || explanation.trim().length < 10">{{ busy() ? 'Checking...' : 'Check my explanation' }}</button>
          </section>
        }

        <!-- PRACTICE / REVISION -->
        @if (d.task.kind === 'Practice' || d.task.kind === 'Revision') {
          @if (!exercise()) {
            <section class="card stack">
              <h2>Your exercise</h2>
              <p>Write the code yourself. Hints and the tutor are there if you need them, but the more you do alone, the more it counts.</p>
              <button class="btn primary" (click)="startExercise()" [disabled]="busy()">{{ busy() ? 'Preparing...' : 'Start exercise' }}</button>
            </section>
          } @else {
            <section class="card stack">
              <div class="row between"><h2 style="margin:0">Exercise</h2>
                <span class="chip" [class.good]="exercise()!.assist === 'Independent'" [class.warn]="exercise()!.assist !== 'Independent'">Help used: {{ exercise()!.assist }}</span></div>
              <p class="wrap" style="white-space:pre-wrap">{{ exercise()!.prompt }}</p>
              @if (exercise()!.source === 'offline') { <div class="banner warn small">Offline demo tutor: exercises are basic and your code cannot be judged. Add an AI key on the server for real review.</div> }

              <label for="code">Your code ({{ lang(exercise()!.language) }})</label>
              <textarea id="code" class="code" [(ngModel)]="code" spellcheck="false" autocapitalize="off" autocomplete="off" maxlength="20000"></textarea>

              <label class="row" style="font-weight:500"><input type="checkbox" [(ngModel)]="focus"> Focus mode: hide all help, solve it alone</label>

              @if (!focus()) {
                @if (exercise()!.hints.length) {
                  @for (h of exercise()!.hints; track $index) { <div class="hint-box wrap"><strong>Hint {{ $index + 1 }}:</strong> {{ h }}</div> }
                }
                <div class="row">
                  <button class="btn" (click)="hint()" [disabled]="busy() || exercise()!.hintsUsed >= exercise()!.hintsTotal">
                    Hint ({{ exercise()!.hintsUsed }}/{{ exercise()!.hintsTotal }})</button>
                  <button class="btn" (click)="showSolution()" [disabled]="busy() || !exercise()!.canViewSolution" [title]="exercise()!.canViewSolution ? '' : 'Make an attempt first'">Show solution</button>
                </div>
                <div class="row"><input class="grow" type="text" [(ngModel)]="question" maxlength="1500" placeholder="Ask the tutor about the concept" aria-label="Ask the tutor">
                  <button class="btn" (click)="ask()" [disabled]="busy() || question.trim().length < 2">Ask</button></div>
                @if (answer()) { <div class="answer"><strong>Tutor ({{ answerSource() }}):</strong>{{ '\n' }}{{ answer() }}</div> }
                @if (solution()) { <div><strong>Sample solution</strong> (counts as watching only)<pre>{{ solution() }}</pre></div> }
              }
              <button class="btn primary" (click)="submitCode()" [disabled]="busy() || code.trim().length < 1">{{ busy() ? 'Reviewing...' : 'Submit for review' }}</button>
            </section>
          }
        }

        @if (review(); as r) {
          <section class="card stack" aria-live="polite">
            <div class="row between"><h2 style="margin:0">Review</h2>
              <span class="chip" [class.good]="r.verified && r.score >= 70" [class.warn]="!r.verified || r.score < 70">{{ r.verified ? r.verdict : 'not verified' }}</span></div>
            @if (r.verified) { <div class="score">{{ r.score }}<span class="muted small"> / 100</span></div> }
            <p class="wrap">{{ r.summary }}</p>
            @if (credit()) { <div class="banner small">{{ credit() }}</div> }
            @if (r.issues.length) { <div><strong>To fix</strong><ul>@for (i of r.issues; track $index) { <li class="wrap">{{ i }}</li> }</ul></div> }
            @if (r.suggestions.length) { <div><strong>Try next</strong><ul>@for (i of r.suggestions; track $index) { <li class="wrap">{{ i }}</li> }</ul></div> }
            @if (r.complexity) { <p class="small"><strong>Complexity:</strong> {{ r.complexity }}</p> }
            @if (r.nextStep) { <p class="small"><strong>Next step:</strong> {{ r.nextStep }}</p> }
            @if (mastery() !== null) { <p class="small muted">Your proven level for this topic: <strong>{{ mastery() }}/100</strong> ({{ masteryText(mastery()!) }}).</p> }
            @if (taskDone()) { <a class="btn primary" routerLink="/today">Task complete. Back to today</a> }
          </section>
        }
      }
    </main>
  `
})
export class TaskPage implements OnInit {
  id = input.required<string>();
  private api = inject(ApiService);
  private router = inject(Router);

  detail = signal<TaskDetail | null>(null);
  exercise = signal<Exercise | null>(null);
  review = signal<Review | null>(null);
  mastery = signal<number | null>(null);
  taskDone = signal(false); credit = signal('');
  solution = signal(''); answer = signal(''); answerSource = signal('');
  focus = signal(false);
  error = signal(''); busy = signal(false);
  code = ''; explanation = ''; question = ''; minutes = 0;
  label = KIND_LABEL; fmt = fmtMinutes; masteryText = masteryLabel; lang = langName;

  async ngOnInit() {
    try {
      const d = await firstValueFrom(this.api.task(this.id()));
      this.detail.set(d); this.exercise.set(d.exercise); this.minutes = d.task.minutes;
      this.code = d.exercise?.starterCode ?? '';
      if (d.lastReview) this.review.set(d.lastReview);
      if (d.lastContent && d.task.kind === 'Explain') this.explanation = d.lastContent;
      this.taskDone.set(d.task.status === 'Done');
    } catch (e) { this.error.set(errorText(e, 'Could not load this task.')); }
  }

  private async run<T>(fn: () => Promise<T>): Promise<T | undefined> {
    this.busy.set(true); this.error.set('');
    try { return await fn(); } catch (e) { this.error.set(errorText(e)); return undefined; } finally { this.busy.set(false); }
  }

  complete() {
    return this.run(async () => { await firstValueFrom(this.api.completeTask(this.id(), this.minutes)); await this.router.navigateByUrl('/today'); });
  }
  skip() {
    return this.run(async () => { await firstValueFrom(this.api.skipTask(this.id())); await this.router.navigateByUrl('/today'); });
  }
  startExercise() {
    return this.run(async () => { const ex = await firstValueFrom(this.api.startExercise(this.id())); this.exercise.set(ex); this.code = ex.starterCode; });
  }
  hint() { return this.run(async () => this.exercise.set(await firstValueFrom(this.api.hint(this.exercise()!.id)))); }
  showSolution() {
    return this.run(async () => { const r = await firstValueFrom(this.api.solution(this.exercise()!.id)); this.exercise.set(r.exercise); this.solution.set(r.solution); });
  }
  ask() {
    return this.run(async () => {
      const r = await firstValueFrom(this.api.askExercise(this.exercise()!.id, this.question));
      this.answer.set(r.answer); this.answerSource.set(r.source); this.exercise.set(r.exercise); this.question = '';
    });
  }
  submitCode() {
    return this.run(async () => {
      const r: SubmitResult = await firstValueFrom(this.api.submit(this.exercise()!.id, this.code));
      this.review.set(r.review); this.mastery.set(r.mastery); this.taskDone.set(r.taskDone); this.credit.set(r.credit); this.exercise.set(r.exercise);
    });
  }
  submitExplain() {
    return this.run(async () => {
      const r = await firstValueFrom(this.api.explain(this.id(), this.explanation));
      this.review.set(r.review); this.mastery.set(r.mastery); this.taskDone.set(r.taskDone);
    });
  }
}
