import axiosInstance from './axiosInstance';

export interface SmtpGroup {
  id: string;
  name: string;
  description?: string;
  isDefault: boolean;
  isActive: boolean;
  emailProvider: string;
  smtpHost?: string;
  smtpPort: number;
  smtpUsername?: string;
  /** M1 — true when an SMTP password is configured (boolean only; value never returned). */
  smtpPasswordSet?: boolean;
  smtpEnableSsl: boolean;
  smtpTimeout: number;
  sendGridApiKeyMasked?: string;
  brevoApiKeyMasked?: string;
  mailgunApiKeyMasked?: string;
  mailgunDomain?: string;
  fromEmail?: string;
  fromName?: string;
  signatureDesignation?: string;
  signaturePhone?: string;
  companyWebsite?: string;
  signatureImageUrl?: string;
  /** Per-group override. Null = inherit global SystemSettings. */
  delayBetweenMessagesMs?: number | null;
  /** Per-group override. Null = inherit global SystemSettings. */
  maxMessagesPerMinute?: number | null;
  // Day 7 G2 webhook secrets (read-only flags — set via update only)
  sendGridWebhookSecretMasked?: string | null;
  brevoWebhookSecretMasked?: string | null;
  mailgunWebhookSecretMasked?: string | null;
  // Day 7 G3 IMAP polling
  enableInboxPolling?: boolean;
  imapHost?: string | null;
  imapPort?: number;
  imapEnableSsl?: boolean;
  imapUsername?: string | null;
  imapFolder?: string;
  lastImapUid?: number;
  lastInboxPolledAt?: string | null;
  inboxPollingIntervalMinutes?: number | null;
  defaultInboxOwnerUserId?: string | null;
  assignedUserCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSmtpGroup {
  name: string;
  description?: string;
  isDefault: boolean;
  isActive: boolean;
  emailProvider: string;
  smtpHost?: string;
  smtpPort: number;
  smtpUsername?: string;
  smtpPassword?: string;
  smtpEnableSsl: boolean;
  smtpTimeout: number;
  sendGridApiKey?: string;
  brevoApiKey?: string;
  mailgunApiKey?: string;
  mailgunDomain?: string;
  fromEmail?: string;
  fromName?: string;
  signatureDesignation?: string;
  signaturePhone?: string;
  companyWebsite?: string;
  signatureImageUrl?: string;
  /** Per-group override. Null = inherit global SystemSettings. */
  delayBetweenMessagesMs?: number | null;
  /** Per-group override. Null = inherit global SystemSettings. */
  maxMessagesPerMinute?: number | null;
  // Day 7 G2 webhook secrets (send raw on save; backend masks on read)
  sendGridWebhookSecret?: string | null;
  brevoWebhookSecret?: string | null;
  mailgunWebhookSecret?: string | null;
  // Day 7 G3 IMAP polling
  enableInboxPolling?: boolean;
  imapHost?: string | null;
  imapPort?: number;
  imapEnableSsl?: boolean;
  imapUsername?: string | null;
  imapPassword?: string | null;
  imapFolder?: string;
  inboxPollingIntervalMinutes?: number | null;
  defaultInboxOwnerUserId?: string | null;
}

export interface UserAssignment {
  userId: string;
  email: string;
  fullName: string;
  role: string;
  smtpGroupId?: string;
  smtpGroupName?: string;
}

export const smtpGroupsApi = {
  list: () => axiosInstance.get('/admin/smtp-groups'),
  get: (id: string) => axiosInstance.get(`/admin/smtp-groups/${id}`),
  create: (data: CreateSmtpGroup) => axiosInstance.post('/admin/smtp-groups', data),
  clone: (id: string) => axiosInstance.post(`/admin/smtp-groups/${id}/clone`),
  update: (id: string, data: CreateSmtpGroup) => axiosInstance.put(`/admin/smtp-groups/${id}`, data),
  delete: (id: string) => axiosInstance.delete(`/admin/smtp-groups/${id}`),
  setDefault: (id: string) => axiosInstance.post(`/admin/smtp-groups/${id}/set-default`),
  test: (id: string, testEmail: string) => axiosInstance.post(`/admin/smtp-groups/${id}/test`, { testEmail }),
  assignUsers: (groupId: string, userIds: string[]) =>
    axiosInstance.post('/admin/smtp-groups/assign-users', { groupId, userIds }),
  unassignUsers: (userIds: string[]) =>
    axiosInstance.post('/admin/smtp-groups/unassign-users', userIds),
  userAssignments: () => axiosInstance.get('/admin/smtp-groups/user-assignments'),
  // L2 — WhatsApp approved templates (Meta Business)
  syncWhatsAppTemplates: (id: string) => axiosInstance.post(`/admin/smtp-groups/${id}/whatsapp-templates/sync`),
  listWhatsAppTemplates: (id: string) => axiosInstance.get(`/admin/smtp-groups/${id}/whatsapp-templates`),
};

export interface WhatsAppTemplate {
  id: string;
  name: string;
  language: string;
  category: string;
  status: string;
  bodyText: string;
  variableCount: number;
  headerType?: string | null;
  syncedAt: string;
}
