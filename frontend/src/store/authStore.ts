import { create } from 'zustand';
import type { UserDto } from '../types/contact.types';

interface AuthState {
  user: UserDto | null;
  accessToken: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  setAuth: (user: UserDto, accessToken: string, refreshToken: string) => void;
  logout: () => void;
  initialize: () => void;
}

// Read from localStorage synchronously at store creation — fixes ProtectedRoute race condition
const _accessToken = localStorage.getItem('accessToken');
const _refreshToken = localStorage.getItem('refreshToken');
const _userStr = localStorage.getItem('user');
let _user: UserDto | null = null;
if (_accessToken && _userStr) {
  try { _user = JSON.parse(_userStr); } catch { /* ignore */ }
}

export const useAuthStore = create<AuthState>((set) => ({
  user: _user,
  accessToken: _accessToken,
  refreshToken: _refreshToken,
  isAuthenticated: !!(_accessToken && _user),

  setAuth: (user, accessToken, refreshToken) => {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
    localStorage.setItem('user', JSON.stringify(user));
    set({ user, accessToken, refreshToken, isAuthenticated: true });
  },

  logout: () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
    set({ user: null, accessToken: null, refreshToken: null, isAuthenticated: false });
  },

  initialize: () => {
    const accessToken = localStorage.getItem('accessToken');
    const refreshToken = localStorage.getItem('refreshToken');
    const userStr = localStorage.getItem('user');
    if (accessToken && userStr) {
      try {
        const user = JSON.parse(userStr) as UserDto;
        set({ user, accessToken, refreshToken, isAuthenticated: true });
      } catch {
        set({ user: null, accessToken: null, refreshToken: null, isAuthenticated: false });
      }
    }
  },
}));
