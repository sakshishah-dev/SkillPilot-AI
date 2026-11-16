import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { TimerService } from './core/timer.service';
import { secondsToClock } from './shared/util';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    @if (auth.isSignedIn()) {
      <nav class="nav" aria-label="Main">
        <a routerLink="/today" routerLinkActive="active">Today</a>
        <a routerLink="/week" routerLinkActive="active">Plan</a>
        <a routerLink="/progress" routerLinkActive="active">Skills</a>
        <a routerLink="/review" routerLinkActive="active">Review</a>
        <a routerLink="/tutor" routerLinkActive="active">Tutor</a>
        <a routerLink="/settings" routerLinkActive="active">Settings</a>
      </nav>
      @if (timer.nudge(); as msg) {
        <div class="toast" role="status">
          <span class="grow">{{ msg }}</span>
          <button class="btn small" (click)="timer.dismissNudge()">OK</button>
        </div>
      }
      @if (timer.running() && !onToday()) {
        <a class="banner good row between" style="margin:12px 16px 0;text-decoration:none;color:inherit" routerLink="/today">
          <span>Study timer running</span><strong>{{ clock(timer.seconds()) }}</strong>
        </a>
      }
    }
    <router-outlet />
  `
})
export class AppComponent {
  auth = inject(AuthService);
  timer = inject(TimerService);
  private router = inject(Router);
  clock = secondsToClock;
  onToday() { return this.router.url.startsWith('/today'); }
}
