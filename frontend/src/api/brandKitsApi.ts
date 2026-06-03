import axiosInstance from './axiosInstance';

export interface BrandKit {
  id: string;
  name: string;
  logoUrl?: string | null;
  primaryColor: string;
  secondaryColor?: string | null;
  accentColor?: string | null;
  fontFamily: string;
  isDefault: boolean;
  createdAt: string;
}

export interface BrandKitPayload {
  name: string;
  logoUrl?: string | null;
  primaryColor: string;
  secondaryColor?: string | null;
  accentColor?: string | null;
  fontFamily: string;
  isDefault: boolean;
}

export const brandKitsApi = {
  list: () => axiosInstance.get('/brand-kits'),
  create: (data: BrandKitPayload) => axiosInstance.post('/brand-kits', data),
  update: (id: string, data: BrandKitPayload) => axiosInstance.put(`/brand-kits/${id}`, data),
  remove: (id: string) => axiosInstance.delete(`/brand-kits/${id}`),
};
