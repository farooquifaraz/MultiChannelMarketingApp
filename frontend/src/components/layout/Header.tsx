import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogOut, User, Sun, Moon } from 'lucide-react';
import { useAuthStore } from '../../store/authStore';
import { authApi } from '../../api/authApi';
import toast from 'react-hot-toast';
import NotificationBell from './NotificationBell';
import { getTheme, toggleTheme } from '../../lib/theme';

export default function Header() {
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

  return (
    <header className="h-16 bg-white border-b border-gray-200 flex items-center justify-between px-6">
      <div>
        <h2 className="text-sm text-gray-500">Welcome back,</h2>
        <p className="text-base font-semibold text-gray-900">{user?.fullName || 'User'}</p>
      </div>
      <div className="flex items-center gap-3">
        <button
          onClick={() => setTheme(toggleTheme())}
          className="p-2 text-gray-500 hover:text-primary-600 hover:bg-gray-100 rounded-xl transition-all"
          title={theme === 'midnight' ? 'Switch to Gold (light)' : 'Switch to Midnight (dark)'}
        >
          {theme === 'midnight' ? <Sun className="w-5 h-5" /> : <Moon className="w-5 h-5" />}
        </button>
        <NotificationBell />
        <button
          onClick={() => navigate('/profile')}
          className="flex items-center gap-2 px-3 py-1.5 bg-gray-50 hover:bg-gray-100 rounded-xl transition-colors"
          title="My Profile"
        >
          <div className="w-8 h-8 bg-primary-100 rounded-lg flex items-center justify-center">
            <User className="w-4 h-4 text-primary-600" />
          </div>
          <span className="text-sm font-medium text-gray-700">{user?.email}</span>
        </button>
        <button
          onClick={handleLogout}
          className="p-2 text-gray-500 hover:text-red-600 hover:bg-red-50 rounded-xl transition-all"
          title="Logout"
        >
          <LogOut className="w-5 h-5" />
        </button>
      </div>
    </header>
  );
}
