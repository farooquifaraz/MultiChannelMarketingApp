import axiosInstance from './axiosInstance';

export interface CredentialRow {
  provider: string;
  hasKey: boolean;
  keyMasked?: string | null;
  model?: string | null;
  baseUrl?: string | null;
  secondarySecretSet: boolean;
  isActive: boolean;
}

export interface CategoryCredentials {
  category: string;
  activeProvider: string;
  credentials: CredentialRow[];
}

export interface SaveCredentialPayload {
  apiKey?: string | null;
  model?: string | null;
  baseUrl?: string | null;
  secondarySecret?: string | null;
}

export const integrationsApi = {
  get: (category: string) => axiosInstance.get(`/admin/integrations/${category}`),
  saveKey: (category: string, provider: string, data: SaveCredentialPayload) =>
    axiosInstance.put(`/admin/integrations/${category}/${provider}`, data),
  activate: (category: string, provider: string) =>
    axiosInstance.post(`/admin/integrations/${category}/${provider}/activate`),
};
