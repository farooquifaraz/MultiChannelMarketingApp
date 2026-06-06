import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  Send,
  MessageSquare,
  Users,
  FileText,
  Settings,
  Zap,
  Server,
  ShieldAlert,
  Clock,
  Inbox,
  CreditCard,
  Building2,
  Image,
  PenLine,
} from 'lucide-react';
import { useAuthStore } from '../../store/authStore';
import { useBrand } from '../../hooks/useBrand';

const baseNav = [
  { to: '/dashboard', icon: LayoutDashboard, label: 'Dashboard', adminOnly: false },
  { to: '/send', icon: MessageSquare, label: 'Send Message', adminOnly: false },
  { to: '/campaigns', icon: Send, label: 'Campaigns', adminOnly: false },
  { to: '/inbox', icon: Inbox, label: 'Inbox', adminOnly: false },
  { to: '/contacts', icon: Users, label: 'Contacts', adminOnly: false },
  { to: '/templates', icon: FileText, label: 'Templates', adminOnly: false },
  { to: '/creatives', icon: Image, label: 'Banner Studio', adminOnly: false },
  { to: '/content', icon: PenLine, label: 'AI Copywriter', adminOnly: false },
  { to: '/admin/users', icon: Users, label: 'Users', adminOnly: true },
  { to: '/admin/smtp-groups', icon: Server, label: 'SMTP Groups', adminOnly: true },
  { to: '/campaigns/scheduled', icon: Clock, label: 'Scheduled', adminOnly: false },
  { to: '/admin/audit-logs', icon: ShieldAlert, label: 'Audit Logs', adminOnly: true },
  { to: '/admin/organizations', icon: Building2, label: 'Organizations', adminOnly: true },
  // Integrations now lives inside Settings → Integrations tab (no separate sidebar item).
  { to: '/billing', icon: CreditCard, label: 'Plan & Usage', adminOnly: false },
  { to: '/settings', icon: Settings, label: 'Settings', adminOnly: false },
];

export default function Sidebar() {
  const user = useAuthStore((s) => s.user);
  const brand = useBrand();
  const isAdmin = user?.role?.toLowerCase() === 'admin';
  const navItems = baseNav.filter(n => !n.adminOnly || isAdmin);

  return (
    <aside className="w-64 bg-white border-r border-gray-200 flex flex-col">
      {/* Logo */}
      <div className="h-16 flex items-center px-6 border-b border-gray-200">
        <div className="flex items-center gap-2">
          {brand.logoUrl ? (
            <img src={brand.logoUrl} alt={brand.name} className="w-8 h-8 rounded-lg object-cover" />
          ) : (
            <div className="w-8 h-8 bg-gradient-to-br from-primary-500 to-primary-700 rounded-lg flex items-center justify-center">
              <Zap className="w-5 h-5 text-white" />
            </div>
          )}
          <span className="text-lg font-bold text-gray-900">{brand.name}</span>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 py-4 px-3 space-y-1">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              `flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all duration-200 ${
                isActive
                  ? 'bg-primary-50 text-primary-700 shadow-sm'
                  : 'text-gray-600 hover:bg-gray-100 hover:text-gray-900'
              }`
            }
          >
            <item.icon className="w-5 h-5" />
            {item.label}
          </NavLink>
        ))}
      </nav>

      {/* Footer */}
      <div className="p-4 border-t border-gray-200">
        <div className="bg-gradient-to-r from-primary-500 to-primary-600 rounded-xl p-4 text-white">
          <p className="text-sm font-semibold">Multi-Channel</p>
          <p className="text-xs opacity-80 mt-1">Email / WhatsApp / SMS</p>
        </div>
      </div>
    </aside>
  );
}
