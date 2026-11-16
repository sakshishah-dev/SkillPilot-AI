import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { errorText } from '../core/auth.interceptor';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  template: `
    <main class="page" style="max-width:440px;padding-top:48px">
      <h1>SkillPilot AI</h1>
      <p class="muted">Your personal tutor. It plans each day, teaches, checks your real progress and adjusts when life gets in the way.</p>
      <form class="card stack" (ngSubmit)="submit()">
        <h2>{{ register() ? 'Create your account' : 'Sign in' }}</h2>
        @if (register()) {
          <div><label for="name">Your name</label><input id="name" type="text" name="name" [(ngModel)]="name" maxlength="80" required autocomplete="name"></div>
        }
        <div><label for="email">Email</label><input id="email" type="email" name="email" [(ngModel)]="email" required autocomplete="email"></div>
        <div><label for="pw">Password</label>
          <input id="pw" type="password" name="pw" [(ngModel)]="password" required minlength="8" [attr.autocomplete]="register() ? 'new-password' : 'current-password'">
          @if (register()) { <span class="muted small">At least 8 characters.</span> }
        </div>
        @if (error()) { <div class="banner bad" role="alert">{{ error() }}</div> }
        <button class="btn primary" type="submit" [disabled]="busy()">{{ busy() ? 'Please wait...' : (register() ? 'Create account' : 'Sign in') }}</button>
        <button class="btn" type="button" (click)="register.set(!register()); error.set('')">
          {{ register() ? 'I already have an account' : 'Create an account' }}
        </button>
      </form>
    </main>
  `
})
export class LoginPage {
  private auth = inject(AuthService);
  private router = inject(Router);
  email = ''; password = ''; name = '';
  register = signal(false); busy = signal(false); error = signal('');

  async submit() {
    if (this.busy()) return;
    this.error.set(''); this.busy.set(true);
    try {
      if (this.register()) {
        await this.auth.register(this.email, this.password, this.name || 'Learner');
        await this.router.navigateByUrl('/welcome');
      } else {
        await this.auth.login(this.email, this.password);
        await this.router.navigateByUrl('/today');
      }
    } catch (e) {
      this.error.set(errorText(e, 'Could not sign in.'));
    } finally { this.busy.set(false); }
  }
}
