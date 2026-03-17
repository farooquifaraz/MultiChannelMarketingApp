import axiosInstance from './axiosInstance';

export interface Notification {
  id: string;
  title: string;
  message: string;
  type: 'info' | 'success' | 'warning' | 'error';
  relatedEntity?: string;
  relatedEntityId?: string;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationCount {
  totalUnread: number;
  infoCount: number;
  successCount: number;
  warningCount: number;
  errorCount: number;
}

export const notificationsApi = {
  getNotifications: (count = 50) => axiosInstance.get(`/notifications?count=${count}`),
  getUnreadCount: () => axiosInstance.get('/notifications/unread-count'),
  markAsRead: (id: string) => axiosInstance.put(`/notifications/${id}/read`),
  markAllAsRead: () => axiosInstance.put('/notifications/read-all'),
};
