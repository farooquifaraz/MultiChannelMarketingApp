import { useNavigate } from 'react-router-dom';
import { LogOut, User } from 'lucide-react';
import { useAuthStore } from '../../store/authStore';
import { authApi } from '../../api/authApi';
import toast from 'react-hot-toast';
import NotificationBell from './NotificationBell';

export default function Header() {
  const { user, refreshToken, logout } = useAuthStore();
  const navigate = useNavigate();

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
        <NotificationBell />
        <div className="flex items-center gap-2 px-3 py-1.5 bg-gray-50 rounded-xl">
          <div className="w-8 h-8 bg-primary-100 rounded-lg flex items-center justify-center">
            <User className="w-4 h-4 text-primary-600" />
          </div>
          <span className="text-sm font-medium text-gray-700">{user?.email}</span>
        </div>
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
