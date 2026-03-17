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
}

export const settingsApi = {
  getSmtpSettings: () => axiosInstance.get('/settings/smtp'),
  saveSmtpSettings: (data: CreateSmtpSettings) => axiosInstance.post('/settings/smtp', data),
  testSmtpConnection: (testEmail: string) => axiosInstance.post('/settings/smtp/test', { testEmail }),
};
