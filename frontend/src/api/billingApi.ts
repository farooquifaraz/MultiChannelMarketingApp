import axiosInstance from './axiosInstance';

export interface Plan {
  code: string;
  name: string;
  priceAedMonthly: number;
  maxContacts: number;
  maxEmailsPerMonth: number;
  maxWhatsAppPerMonth: number;
  maxAiPerMonth: number;
  maxUsers: number;
  sortOrder: number;
}

export interface UsageMetric {
  used: number;
  limit: number;        // -1 = unlimited
  unlimited: boolean;
  remaining: number;
  percent: number;
  overLimit: boolean;
}

export interface Subscription {
  planCode: string;
  planName: string;
  priceAedMonthly: number;
  status: string;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  quotasEnforced: boolean;
  contacts: UsageMetric;
  emails: UsageMetric;
  whatsApp: UsageMetric;
  ai: UsageMetric;
}

export const billingApi = {
  plans: () => axiosInstance.get('/billing/plans'),
  subscription: () => axiosInstance.get('/billing/subscription'),
  changePlan: (planCode: string) => axiosInstance.post('/billing/subscription/change', { planCode }),
};
