import axiosInstance from './axiosInstance';

export interface AdminUser {
  id: string;
  fullName: string;
  email: string;
  role: 'admin' | 'user';
  isActive: boolean;
  smtpGroupId?: string | null;
  smtpGroupName?: string | null;
  createdAt: string;
  updatedAt: string;
  hasPersonalSignature: boolean;
}

export interface CreateUserPayload {
  fullName: string;
  email: string;
  password: string;
  role: 'admin' | 'user';
  smtpGroupId?: string | null;
}

export interface UpdateUserPayload {
  fullName: string;
  email: string;
  smtpGroupId?: string | null;
  isActive: boolean;
}

export const adminUsersApi = {
  list: (params: { role?: string; smtpGroupId?: string; search?: string; isActive?: boolean } = {}) =>
    axiosInstance.get('/admin/users', { params }),
  get: (id: string) => axiosInstance.get(`/admin/users/${id}`),
  create: (data: CreateUserPayload) => axiosInstance.post('/admin/users', data),
  update: (id: string, data: UpdateUserPayload) => axiosInstance.put(`/admin/users/${id}`, data),
  changeRole: (id: string, role: 'admin' | 'user') => axiosInstance.put(`/admin/users/${id}/role`, { role }),
  resetPassword: (id: string, newPassword: string) =>
    axiosInstance.post(`/admin/users/${id}/reset-password`, { newPassword }),
  delete: (id: string) => axiosInstance.delete(`/admin/users/${id}`),
};
