import axiosInstance from './axiosInstance';

export interface SmtpSettings {
  id: string;
  smtpHost: string;
  smtpPort: number;
  smtpUsername: string;
  smtpFromEmail: string;
  smtpFromName: string;
  smtpEnableSsl: boolean;
  smtpUseDefaultCredentials: boolean;
  smtpTimeout: number;
  emailProvider: string;
  sendGridApiKey: string;
  brevoApiKey: string;
  mailgunApiKey: string;
  mailgunDomain: string;
  whatsAppApiKey: string;
  whatsAppPhoneNumberId: string;
  whatsAppBusinessAccountId: string;
  smsApiKey: string;
  smsSenderNumber: string;
  notifyOnCampaignComplete: boolean;
  notifyOnMessageFailed: boolean;
  notificationEmail: string;
  // Signature
  signatureDesignation?: string;
  signaturePhone?: string;
  companyWebsite?: string;
  signatureImageUrl?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSmtpSettings {
  smtpHost: string;
  smtpPort: number;
  smtpUsername: string;
  smtpPassword: string;
  smtpFromEmail: string;
  smtpFromName?: string;
  smtpEnableSsl: boolean;
  smtpUseDefaultCredentials: boolean;
  smtpTimeout: number;
  whatsAppApiKey?: string;
  whatsAppPhoneNumberId?: string;
  whatsAppBusinessAccountId?: string;
  smsApiKey?: string;
  smsApiSecret?: string;
  smsSenderNumber?: string;
  emailProvider: string;
  sendGridApiKey?: string;
  brevoApiKey?: string;
  mailgunApiKey?: string;
  mailgunDomain?: string;
  notifyOnCampaignComplete: boolean;
  notifyOnMessageFailed: boolean;
  notificationEmail?: string;
  // Signature
  signatureDesignation?: string;
  signaturePhone?: string;
  companyWebsite?: string;
  signatureImageUrl?: string;
}

export interface SystemSettings {
  batchSize: number;
  delayBetweenBatchesMs: number;
  delayBetweenMessagesMs: number;
  maxMessagesPerMinute: number;
  allowUsersToSeeSharedTemplates: boolean;
  allowUsersToSeeSharedContacts: boolean;
  // Branding
  platformName: string;
  logoUrl?: string | null;
  primaryColor: string;
  avatarServiceUrl: string;
  // Localization
  defaultLocale: string;
  defaultDateFormat: string;
  // Limits
  passwordMinLength: number;
  maxFileUploadSizeMb: number;
  // Day 7 G3
  inboxPollingCron?: string;
  // Day 7 G5 — AI (apiKey returned MASKED; send raw on update)
  aiProvider?: string;
  aiApiKey?: string | null;            // for update payload
  aiApiKeyMasked?: string | null;      // for GET response
  aiBaseUrl?: string | null;
  aiModel?: string;
  aiSystemPrompt?: string;
  aiMaxTokens?: number;
  aiTemperature?: number;
  aiTimeoutSeconds?: number;
  aiAllowSendRecipientPii?: boolean;
  aiAllowedCategoriesCsv?: string;
  // Day 10 — fallback provider (auto-failover on quota)
  aiFallbackProvider?: string;
  aiFallbackApiKey?: string | null;        // update payload
  aiFallbackApiKeyMasked?: string | null;  // GET response
  aiFallbackBaseUrl?: string | null;
  aiFallbackModel?: string;
  updatedAt: string;
}

export const adminApi = {
  getSystemSettings: () => axiosInstance.get('/admin/system-settings'),
  updateSystemSettings: (data: SystemSettings) => axiosInstance.put('/admin/system-settings', data),
  // Day 7 G5 — AI helpers
  getAiDefaultPrompt: () => axiosInstance.get('/admin/ai/default-prompt'),
  testAi: (data: any) => axiosInstance.post('/admin/ai/test', data),
};

export const settingsApi = {
  getSmtpSettings: () => axiosInstance.get('/settings/smtp'),
  saveSmtpSettings: (data: CreateSmtpSettings) => axiosInstance.post('/settings/smtp', data),
  testSmtpConnection: (testEmail: string) => axiosInstance.post('/settings/smtp/test', { testEmail }),
  uploadSignatureImage: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return axiosInstance.post('/settings/signature-image/upload', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

export interface MySignature {
  fullName: string;
  signatureDesignation?: string;
  signaturePhone?: string;
  signatureImageUrl?: string;
  effectiveDesignation?: string;
  effectivePhone?: string;
  effectiveImageUrl?: string;
  orgFromEmail?: string;
  orgCompanyName?: string;
  orgCompanyWebsite?: string;
  orgSmtpGroupName?: string;
}

export const meApi = {
  getSignature: () => axiosInstance.get('/me/signature'),
  saveSignature: (data: { signatureDesignation?: string; signaturePhone?: string; signatureImageUrl?: string }) =>
    axiosInstance.put('/me/signature', data),
  uploadImage: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return axiosInstance.post('/me/signature/image/upload', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};
