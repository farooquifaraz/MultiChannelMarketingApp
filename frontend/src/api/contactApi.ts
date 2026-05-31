import axiosInstance from './axiosInstance';
import type { ApiResponse, PagedResponse } from '../types/api.types';
import type { ContactDto, CreateContactDto, ContactGroupDto, ImportResultDto } from '../types/contact.types';

export const contactApi = {
  getAll: (params: { pageNumber?: number; pageSize?: number; groupId?: string; search?: string }) =>
    axiosInstance.get<any, PagedResponse<ContactDto>>('/contacts', { params }),

  getById: (id: string) =>
    axiosInstance.get<any, ApiResponse<ContactDto>>(`/contacts/${id}`),

  create: (data: CreateContactDto) =>
    axiosInstance.post<any, ApiResponse<ContactDto>>('/contacts', data),

  update: (id: string, data: Partial<CreateContactDto>) =>
    axiosInstance.put<any, ApiResponse<ContactDto>>(`/contacts/${id}`, data),

  delete: (id: string) =>
    axiosInstance.delete(`/contacts/${id}`),

  import: (file: File, groupId?: string) => {
    const formData = new FormData();
    formData.append('file', file);
    return axiosInstance.post<any, ApiResponse<ImportResultDto>>(
      `/contacts/import${groupId ? `?groupId=${groupId}` : ''}`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } }
    );
  },

  getGroups: () =>
    axiosInstance.get<any, ApiResponse<ContactGroupDto[]>>('/contacts/groups'),

  createGroup: (data: { name: string; description?: string; shareWithTeam?: boolean }) =>
    axiosInstance.post<any, ApiResponse<ContactGroupDto>>('/contacts/groups', data),

  updateGroup: (id: string, data: { name: string; description?: string; shareWithTeam?: boolean; smtpGroupId?: string | null }) =>
    axiosInstance.put<any, ApiResponse<ContactGroupDto>>(`/contacts/groups/${id}`, data),

  deleteGroup: (id: string) =>
    axiosInstance.delete(`/contacts/groups/${id}`),

  assignToGroup: (contactIds: string[], groupId: string | null) =>
    axiosInstance.post<any, ApiResponse<{ assignedCount: number }>>('/contacts/assign-group', { contactIds, groupId }),

  clearBounce: (id: string) =>
    axiosInstance.post<any, ApiResponse<ContactDto>>(`/contacts/${id}/clear-bounce`),

  exportCsvUrl: (params: { groupId?: string; search?: string } = {}) => {
    const q = new URLSearchParams();
    if (params.groupId) q.set('groupId', params.groupId);
    if (params.search) q.set('search', params.search);
    const qs = q.toString();
    return `/contacts/export.csv${qs ? '?' + qs : ''}`;
  },
};
