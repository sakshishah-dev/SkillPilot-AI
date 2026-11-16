import { Routes } from '@angular/router';
import { authGuard } from './core/guards';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login.page').then(m => m.LoginPage) },
  { path: 'welcome', canActivate: [authGuard], loadComponent: () => import('./pages/onboarding.page').then(m => m.OnboardingPage) },
  { path: 'today', canActivate: [authGuard], loadComponent: () => import('./pages/today.page').then(m => m.TodayPage) },
  { path: 'task/:id', canActivate: [authGuard], loadComponent: () => import('./pages/task.page').then(m => m.TaskPage) },
  { path: 'week', canActivate: [authGuard], loadComponent: () => import('./pages/week.page').then(m => m.WeekPage) },
  { path: 'progress', canActivate: [authGuard], loadComponent: () => import('./pages/progress.page').then(m => m.ProgressPage) },
  { path: 'review', canActivate: [authGuard], loadComponent: () => import('./pages/review.page').then(m => m.ReviewPage) },
  { path: 'tutor', canActivate: [authGuard], loadComponent: () => import('./pages/tutor.page').then(m => m.TutorPage) },
  { path: 'settings', canActivate: [authGuard], loadComponent: () => import('./pages/settings.page').then(m => m.SettingsPage) },
  { path: '', pathMatch: 'full', redirectTo: 'today' },
  { path: '**', redirectTo: 'today' }
];
