import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, CheckCheck, Info, CheckCircle2, AlertTriangle, XCircle, X } from 'lucide-react';
import { notificationsApi, type Notification, type NotificationCount } from '../../api/notificationsApi';

export default function NotificationBell() {
  const navigate = useNavigate();
  const [isOpen, setIsOpen] = useState(false);
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [unreadCount, setUnreadCount] = useState<NotificationCount | null>(null);
  const [loading, setLoading] = useState(false);
  const [filter, setFilter] = useState<'all' | 'unread'>('all');
  const dropdownRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    fetchUnreadCount();
    const interval = setInterval(fetchUnreadCount, 30000); // Poll every 30s
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const fetchUnreadCount = async () => {
    try {
      const res: any = await notificationsApi.getUnreadCount();
      setUnreadCount(res?.data || null);
    } catch {
      // Silently fail
    }
  };

  const fetchNotifications = async () => {
    setLoading(true);
    try {
      const res: any = await notificationsApi.getNotifications(20);
      setNotifications(res?.data || []);
    } catch {
      // Silently fail
    } finally {
      setLoading(false);
    }
  };

  const handleToggle = () => {
    const next = !isOpen;
    setIsOpen(next);
    if (next) fetchNotifications();
  };

  const handleMarkAsRead = async (id: string) => {
    try {
      await notificationsApi.markAsRead(id);
      setNotifications(prev => prev.map(n => n.id === id ? { ...n, isRead: true } : n));
      fetchUnreadCount();
    } catch {}
  };

  const handleMarkAllAsRead = async () => {
    try {
      await notificationsApi.markAllAsRead();
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      fetchUnreadCount();
    } catch {}
  };

  const buildEntityUrl = (entity?: string, id?: string): string | null => {
    if (!entity || !id) return null;
    const e = entity.toLowerCase();
    if (e === 'campaign') return `/campaigns/${id}`;
    if (e === 'contact') return `/contacts`;
    if (e === 'contactgroup') return `/contacts`;
    if (e === 'template') return `/templates`;
    if (e === 'smtpgroup') return `/admin/smtp-groups`;
    if (e === 'user') return `/admin/users`;
    // Inbox now uses thread-based URLs (?thread=). The notification carries a message-id, not a
    // thread-id, so just open the inbox — the new reply is sorted to the top + highlighted unread.
    if (e === 'inboxmessage') return `/inbox`;
    return null;
  };

  const handleNotificationClick = async (n: Notification) => {
    if (!n.isRead) await handleMarkAsRead(n.id);
    const url = buildEntityUrl(n.relatedEntity, n.relatedEntityId);
    if (url) {
      setIsOpen(false);
      navigate(url);
    }
  };

  const getTypeIcon = (type: string) => {
    switch (type) {
      case 'success': return <CheckCircle2 className="w-4 h-4 text-green-500 flex-shrink-0" />;
      case 'warning': return <AlertTriangle className="w-4 h-4 text-amber-500 flex-shrink-0" />;
      case 'error': return <XCircle className="w-4 h-4 text-red-500 flex-shrink-0" />;
      default: return <Info className="w-4 h-4 text-blue-500 flex-shrink-0" />;
    }
  };

  const getTypeBg = (type: string) => {
    switch (type) {
      case 'success': return 'bg-green-50 border-green-100';
      case 'warning': return 'bg-amber-50 border-amber-100';
      case 'error': return 'bg-red-50 border-red-100';
      default: return 'bg-blue-50 border-blue-100';
    }
  };

  const timeAgo = (dateStr: string) => {
    const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
    if (seconds < 60) return 'Just now';
    const mins = Math.floor(seconds / 60);
    if (mins < 60) return `${mins}m ago`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return `${days}d ago`;
  };

  const totalUnread = unreadCount?.totalUnread || 0;

  return (
    <div className="relative" ref={dropdownRef}>
      <button
        onClick={handleToggle}
        className="relative p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
      >
        <Bell className="w-5 h-5" />
        {totalUnread > 0 && (
          <span className="absolute -top-0.5 -right-0.5 flex items-center justify-center min-w-[18px] h-[18px] px-1 bg-red-500 text-white text-[10px] font-bold rounded-full animate-pulse">
            {totalUnread > 99 ? '99+' : totalUnread}
          </span>
        )}
      </button>

      {isOpen && (
        <div className="absolute right-0 mt-2 w-96 bg-white rounded-xl shadow-xl border border-gray-100 z-50 overflow-hidden">
          {/* Header */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50">
            <div className="flex items-center gap-2">
              <h3 className="font-semibold text-gray-900 text-sm">Notifications</h3>
              {totalUnread > 0 && (
                <span className="px-2 py-0.5 bg-blue-100 text-blue-700 text-xs font-medium rounded-full">{totalUnread} new</span>
              )}
            </div>
            <div className="flex items-center gap-1">
              {totalUnread > 0 && (
                <button
                  onClick={handleMarkAllAsRead}
                  className="flex items-center gap-1 text-xs text-blue-600 hover:text-blue-800 px-2 py-1 rounded hover:bg-blue-50 transition-colors"
                >
                  <CheckCheck className="w-3.5 h-3.5" />
                  Mark all read
                </button>
              )}
              <button onClick={() => setIsOpen(false)} className="p-1 text-gray-400 hover:text-gray-600 rounded hover:bg-gray-200 transition-colors">
                <X className="w-4 h-4" />
              </button>
            </div>
          </div>

          {/* Filter Tabs */}
          <div className="flex border-b border-gray-100 bg-white">
            <button
              onClick={() => setFilter('all')}
              className={`flex-1 px-4 py-2 text-xs font-medium transition-colors ${
                filter === 'all' ? 'text-primary-700 border-b-2 border-primary-600' : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              All ({notifications.length})
            </button>
            <button
              onClick={() => setFilter('unread')}
              className={`flex-1 px-4 py-2 text-xs font-medium transition-colors ${
                filter === 'unread' ? 'text-primary-700 border-b-2 border-primary-600' : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              Unread ({notifications.filter(n => !n.isRead).length})
            </button>
          </div>

          {/* Notifications List */}
          <div className="max-h-96 overflow-y-auto">
            {loading ? (
              <div className="py-8 text-center text-gray-400 text-sm">Loading...</div>
            ) : notifications.length === 0 ? (
              <div className="py-8 text-center">
                <Bell className="w-10 h-10 text-gray-200 mx-auto mb-2" />
                <p className="text-gray-400 text-sm">No notifications yet</p>
              </div>
            ) : (() => {
                const visible = filter === 'unread' ? notifications.filter(n => !n.isRead) : notifications;
                if (visible.length === 0) {
                  return (
                    <div className="py-8 text-center">
                      <CheckCheck className="w-10 h-10 text-green-200 mx-auto mb-2" />
                      <p className="text-gray-400 text-sm">All caught up!</p>
                    </div>
                  );
                }
                return visible.map(n => {
                  const targetUrl = buildEntityUrl(n.relatedEntity, n.relatedEntityId);
                  return (
                    <div
                      key={n.id}
                      onClick={() => handleNotificationClick(n)}
                      className={`px-4 py-3 border-b border-gray-50 cursor-pointer transition-colors ${
                        n.isRead ? 'bg-white hover:bg-gray-50' : `${getTypeBg(n.type)} hover:opacity-90`
                      }`}
                    >
                      <div className="flex items-start gap-3">
                        <div className="mt-0.5">{getTypeIcon(n.type)}</div>
                        <div className="flex-1 min-w-0">
                          <div className="flex items-center justify-between">
                            <p className={`text-sm font-medium truncate ${n.isRead ? 'text-gray-700' : 'text-gray-900'}`}>{n.title}</p>
                            <span className="text-[11px] text-gray-400 ml-2 whitespace-nowrap">{timeAgo(n.createdAt)}</span>
                          </div>
                          <p className={`text-xs mt-0.5 line-clamp-2 ${n.isRead ? 'text-gray-400' : 'text-gray-600'}`}>{n.message}</p>
                          {targetUrl && (
                            <p className="text-[11px] text-primary-600 mt-1 font-medium">View details →</p>
                          )}
                        </div>
                        {!n.isRead && <div className="w-2 h-2 bg-blue-500 rounded-full mt-1.5 flex-shrink-0" />}
                      </div>
                    </div>
                  );
                });
              })()
            }
          </div>
        </div>
      )}
    </div>
  );
}
