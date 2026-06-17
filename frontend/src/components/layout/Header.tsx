import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogOut, Sun, Moon, Search, Menu } from 'lucide-react';
import { useAuthStore } from '../../store/authStore';
import { authApi } from '../../api/authApi';
import toast from 'react-hot-toast';
import NotificationBell from './NotificationBell';
import { getTheme, toggleTheme } from '../../lib/theme';

export default function Header({ onMenuClick }: { onMenuClick?: () => void }) {
  const { user, refreshToken, logout } = useAuthStore();
  const navigate = useNavigate();
  const [theme, setTheme] = useState(getTheme());

  const handleLogout = async () => {
    try {
      if (refreshToken) await authApi.logout(refreshToken);
    } catch { /* ignore */ } finally {
      logout();
      navigate('/login');
      toast.success('Logged out');
    }
  };

  const initials = (user?.fullName || user?.email || 'U').split(' ').map((s) => s[0]).join('').slice(0, 2).toUpperCase();

  return (
    <header className="h-16 bg-white border-b border-gray-100 flex items-center gap-4 px-4 md:px-6">
      <button
        onClick={onMenuClick}
        className="lg:hidden w-10 h-10 grid place-items-center rounded-xl bg-gray-50 border border-gray-100 text-gray-500 hover:text-primary-600 hover:bg-gray-100 transition-all"
        title="Menu"
        aria-label="Toggle menu"
      >
        <Menu className="w-5 h-5" />
      </button>
      <div>
        <h2 className="text-xs text-gray-500">Welcome back,</h2>
        <p className="text-[15px] font-bold text-gray-900 leading-tight">{user?.fullName || 'User'}</p>
      </div>

      {/* Search */}
      <div className="hidden md:flex items-center gap-2 ml-4 px-3.5 py-2 rounded-xl bg-gray-50 border border-gray-100 text-gray-400 text-sm min-w-[240px]">
        <Search className="w-4 h-4" />
        <span>Search campaigns, contacts…</span>
      </div>

      <div className="flex items-center gap-2 ml-auto">
        <button
          onClick={() => setTheme(toggleTheme())}
          className="w-10 h-10 grid place-items-center rounded-xl bg-gray-50 border border-gray-100 text-gray-500 hover:text-primary-600 hover:bg-gray-100 transition-all"
          title={theme === 'midnight' ? 'Switch to Gold (light)' : 'Switch to Midnight (dark)'}
        >
          {theme === 'midnight' ? <Sun className="w-5 h-5" /> : <Moon className="w-5 h-5" />}
        </button>
        <NotificationBell />
        <button
          onClick={() => navigate('/profile')}
          className="flex items-center gap-2.5 pl-1 pr-3 py-1 bg-gray-50 hover:bg-gray-100 border border-gray-100 rounded-xl transition-colors"
          title="My Profile"
        >
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-primary-400 to-primary-700 grid place-items-center text-white text-xs font-bold">{initials}</div>
          <span className="text-sm font-medium text-gray-700 hidden sm:block max-w-[160px] truncate">{user?.email}</span>
        </button>
        <button
          onClick={handleLogout}
          className="w-10 h-10 grid place-items-center rounded-xl bg-gray-50 border border-gray-100 text-gray-500 hover:text-red-600 hover:bg-red-50 transition-all"
          title="Logout"
        >
          <LogOut className="w-5 h-5" />
        </button>
      </div>
    </header>
  );
}
