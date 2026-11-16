import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { API_BASE } from './config';
import { AuthResult } from './models';

const TOKEN_KEY = 'sp_token';
const USER_KEY = 'sp_user';

function read(key: string): string | null {
  try { return localStorage.getItem(key); } catch { return null; }
}
function write(key: string, value: string | null) {
  try { value === null ? localStorage.removeItem(key) : localStorage.setItem(key, value); } catch { /* storage may be blocked */ }
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private _token = signal<string | null>(read(TOKEN_KEY));
  private _user = signal<AuthResult['user'] | null>(this.readUser());

  readonly user = this._user.asReadonly();
  readonly isSignedIn = computed(() => !!this._token());

  get token() { return this._token(); }

  private readUser() {
    const raw = read(USER_KEY);
    try { return raw ? JSON.parse(raw) : null; } catch { return null; }
  }

  private store(res: AuthResult) {
    write(TOKEN_KEY, res.token);
    write(USER_KEY, JSON.stringify(res.user));
    this._token.set(res.token);
    this._user.set(res.user);
  }

  async login(email: string, password: string) {
    this.store(await firstValueFrom(this.http.post<AuthResult>(`${API_BASE}/auth/login`, { email, password })));
  }

  async register(email: string, password: string, displayName: string) {
    this.store(await firstValueFrom(this.http.post<AuthResult>(`${API_BASE}/auth/register`, { email, password, displayName })));
  }

  logout() {
    write(TOKEN_KEY, null); write(USER_KEY, null);
    this._token.set(null); this._user.set(null);
    this.router.navigateByUrl('/login');
  }
}
