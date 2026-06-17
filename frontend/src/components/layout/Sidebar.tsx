import { NavLink, Link } from 'react-router-dom';
import {
  LayoutDashboard, Send, MessageSquare, Users, FileText, Settings, Zap,
  Server, ShieldAlert, Clock, Inbox, CreditCard, Building2, Sparkles, Crown,
} from 'lucide-react';
import { useAuthStore } from '../../store/authStore';
import { useBrand } from '../../hooks/useBrand';

type NavItem = { to: string; icon: any; label: string; adminOnly?: boolean };

const SECTIONS: { title?: string; items: NavItem[] }[] = [
  {
    items: [
      { to: '/dashboard', icon: LayoutDashboard, label: 'Dashboard' },
      { to: '/send', icon: MessageSquare, label: 'Send Message' },
      { to: '/campaigns', icon: Send, label: 'Campaigns' },
      { to: '/inbox', icon: Inbox, label: 'Inbox' },
      { to: '/studio', icon: Sparkles, label: 'Creative Studio' },
      { to: '/contacts', icon: Users, label: 'Contacts' },
    ],
  },
  {
    title: 'Manage',
    items: [
      { to: '/templates', icon: FileText, label: 'Templates' },
      { to: '/campaigns/scheduled', icon: Clock, label: 'Scheduled' },
      { to: '/admin/users', icon: Users, label: 'Users', adminOnly: true },
      { to: '/admin/smtp-groups', icon: Server, label: 'SMTP Groups', adminOnly: true },
      { to: '/admin/organizations', icon: Building2, label: 'Organizations', adminOnly: true },
      { to: '/admin/audit-logs', icon: ShieldAlert, label: 'Audit Logs', adminOnly: true },
    ],
  },
  {
    title: 'Account',
    items: [
      { to: '/billing', icon: CreditCard, label: 'Plan & Usage' },
      { to: '/settings', icon: Settings, label: 'Settings' },
    ],
  },
];

export default function Sidebar({ open = false }: { open?: boolean }) {
  const user = useAuthStore((s) => s.user);
  const brand = useBrand();
  const isAdmin = user?.role?.toLowerCase() === 'admin';

  return (
    <aside
      className={`fixed inset-y-0 left-0 z-40 w-64 bg-white border-r border-gray-200 flex flex-col transform transition-transform duration-200 lg:static lg:translate-x-0 ${
        open ? 'translate-x-0' : '-translate-x-full'
      }`}
    >
      {/* Brand */}
      <div className="h-16 flex items-center px-5 border-b border-gray-100">
        <div className="flex items-center gap-2.5">
          {brand.logoUrl ? (
            <img src={brand.logoUrl} alt={brand.name} className="w-9 h-9 rounded-xl object-cover" />
          ) : (
            <div className="w-9 h-9 bg-gradient-to-br from-primary-400 to-primary-700 rounded-xl flex items-center justify-center shadow-md shadow-primary-200">
              <Zap className="w-5 h-5 text-white" />
            </div>
          )}
          <span className="text-[17px] font-bold tracking-tight text-gray-900">{brand.name}</span>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto py-3 px-3 space-y-0.5">
        {SECTIONS.map((section, si) => {
          const items = section.items.filter((n) => !n.adminOnly || isAdmin);
          if (items.length === 0) return null;
          return (
            <div key={si} className={si > 0 ? 'pt-3' : ''}>
              {section.title && (
                <p className="px-3 pb-1.5 text-[10.5px] font-bold uppercase tracking-wider text-gray-400">{section.title}</p>
              )}
              {items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.to === '/campaigns'}
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-3 py-2.5 rounded-xl text-[13.5px] font-semibold transition-all duration-150 ${
                      isActive
                        ? 'bg-primary-50 text-primary-700 shadow-sm shadow-primary-100'
                        : 'text-gray-500 hover:bg-gray-50 hover:text-gray-900'
                    }`
                  }
                >
                  <item.icon className="w-[18px] h-[18px]" />
                  {item.label}
                </NavLink>
              ))}
            </div>
          );
        })}
      </nav>

      {/* Upgrade promo */}
      <div className="p-3">
        <div className="rounded-2xl p-4 text-white bg-gradient-to-br from-primary-500 via-primary-600 to-primary-700 shadow-lg shadow-primary-200">
          <div className="flex items-center gap-2 font-bold text-sm"><Crown className="w-4 h-4" /> Upgrade to Pro</div>
          <p className="text-[11.5px] opacity-90 mt-1 leading-snug">50k emails, AI inbox &amp; tracking.</p>
          <Link to="/billing" className="inline-block mt-2.5 bg-white/20 hover:bg-white/30 transition-colors rounded-lg px-3 py-1.5 text-[12px] font-bold">See plans →</Link>
        </div>
      </div>
    </aside>
  );
}
