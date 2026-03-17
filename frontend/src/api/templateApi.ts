import axiosInstance from './axiosInstance';
import type { ApiResponse } from '../types/api.types';
import type { TemplateDto, CreateTemplateDto } from '../types/contact.types';

export const templateApi = {
  getAll: (channel?: string) =>
    axiosInstance.get<any, ApiResponse<TemplateDto[]>>('/templates', { params: channel ? { channel } : {} }),

  getById: (id: string) =>
    axiosInstance.get<any, ApiResponse<TemplateDto>>(`/templates/${id}`),

  create: (data: CreateTemplateDto) =>
    axiosInstance.post<any, ApiResponse<TemplateDto>>('/templates', data),

  update: (id: string, data: Partial<CreateTemplateDto> & { isActive?: boolean }) =>
    axiosInstance.put<any, ApiResponse<TemplateDto>>(`/templates/${id}`, data),

  delete: (id: string) =>
    axiosInstance.delete(`/templates/${id}`),

  preview: (id: string, sampleData: Record<string, string>) =>
    axiosInstance.post<any, ApiResponse<string>>(`/templates/${id}/preview`, sampleData),
};
