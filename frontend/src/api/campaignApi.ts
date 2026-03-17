import axiosInstance from './axiosInstance';
import type { ApiResponse, PagedResponse } from '../types/api.types';
import type { CampaignDto, CampaignDetailDto, CreateCampaignDto, CampaignReportDto, CampaignMessageDto } from '../types/campaign.types';

export const campaignApi = {
  getAll: (params: { pageNumber?: number; pageSize?: number; status?: string; channel?: string }) =>
    axiosInstance.get<any, PagedResponse<CampaignDto>>('/campaigns', { params }),

  getById: (id: string) =>
    axiosInstance.get<any, ApiResponse<CampaignDetailDto>>(`/campaigns/${id}`),

  create: (data: CreateCampaignDto) =>
    axiosInstance.post<any, ApiResponse<CampaignDto>>('/campaigns', data),

  update: (id: string, data: Partial<CreateCampaignDto>) =>
    axiosInstance.put<any, ApiResponse<CampaignDto>>(`/campaigns/${id}`, data),

  delete: (id: string) =>
    axiosInstance.delete(`/campaigns/${id}`),

  send: (id: string, scheduledAt?: string) =>
    axiosInstance.post(`/campaigns/${id}/send`, scheduledAt ? { scheduledAt } : {}),

  getReport: (id: string) =>
    axiosInstance.get<any, ApiResponse<CampaignReportDto>>(`/campaigns/${id}/report`),

  getMessages: (id: string, params: { pageNumber?: number; pageSize?: number; status?: string }) =>
    axiosInstance.get<any, PagedResponse<CampaignMessageDto>>(`/campaigns/${id}/messages`, { params }),
};
