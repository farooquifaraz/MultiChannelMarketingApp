import axiosInstance from './axiosInstance';
import type { ApiResponse } from '../types/api.types';

export interface MyProfile {
  id: string;
  fullName: string;
  email: string;
  role: string;
  smtpGroupName?: string | null;
  createdAt: string;
}

/**
 * M2 — describes which SmtpGroup will actually deliver this user's outgoing email.
 * Used by Send Message + Send Campaign pages to render the pre-send banner so the
 * user knows whether Brevo, SMTP, SendGrid, or Mailgun is about to fire — and
 * whether credentials are wired up correctly. groupId being null means no default
 * group is configured platform-wide; the UI surfaces that as a hard warning.
 */
export interface ActiveSender {
  groupId: string | null;
  groupName: string | null;
  /** smtp | brevo | sendgrid | mailgun, lowercased */
  provider: string | null;
  fromEmail: string | null;
  fromName: string | null;
  /** true when the resolved group is the platform-wide default */
  isDefault: boolean;
  /** true when the resolved group is explicitly assigned to this user (vs the default fallback) */
  isAssignedToUser: boolean;
  /** Provider-specific: API key configured for Brevo/SendGrid/Mailgun */
  hasApiKey: boolean;
  /** SMTP only: password configured */
  hasSmtpPassword: boolean;
  delayBetweenMessagesMs: number | null;
  maxMessagesPerMinute: number | null;
}

export const meApi = {
  getProfile: () =>
    axiosInstance.get<any, ApiResponse<MyProfile>>('/me/profile'),

  updateProfile: (data: { fullName: string }) =>
    axiosInstance.put<any, ApiResponse<MyProfile>>('/me/profile', data),

  changePassword: (data: { currentPassword: string; newPassword: string }) =>
    axiosInstance.post<any, ApiResponse<null>>('/me/password', data),

  /** M2 — fetch the user's active outbound sender (provider, from-address, credential health). */
  getActiveSender: () =>
    axiosInstance.get<any, ApiResponse<ActiveSender>>('/me/active-sender'),
};
