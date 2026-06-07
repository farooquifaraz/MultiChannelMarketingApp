import axiosInstance from './axiosInstance';

export interface Organization {
  id: string;
  name: string;
  slug: string;
  ownerUserId?: string | null;
  planCode: string;
  isActive: boolean;
  userCount: number;
  isLegacy: boolean;
  createdAt: string;
}

export interface CreateOrganizationPayload {
  name: string;
  slug?: string;
  planCode?: string;
}

export const organizationsApi = {
  list: () => axiosInstance.get('/admin/organizations'),
  create: (data: CreateOrganizationPayload) => axiosInstance.post('/admin/organizations', data),
  update: (id: string, data: { name: string; planCode?: string }) =>
    axiosInstance.put(`/admin/organizations/${id}`, data),
  remove: (id: string) => axiosInstance.delete(`/admin/organizations/${id}`),
  assignUser: (userId: string, organizationId: string) =>
    axiosInstance.post('/admin/organizations/assign-user', { userId, organizationId }),
};
