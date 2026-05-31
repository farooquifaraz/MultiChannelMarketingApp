import axiosInstance from './axiosInstance';
import type { ApiResponse, PagedResponse } from '../types/api.types';
import type { CampaignDto, CampaignDetailDto, CreateCampaignDto, CampaignReportDto, CampaignMessageDto } from '../types/campaign.types';

export const campaignApi = {
  getAll: (params: { pageNumber?: number; pageSize?: number; status?: string; channel?: string; viewAll?: boolean }) =>
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

  cancelSchedule: (id: string) =>
    axiosInstance.post<any, ApiResponse<null>>(`/campaigns/${id}/cancel-schedule`),

  retryFailed: (id: string) =>
    axiosInstance.post<any, ApiResponse<{ retriedCount: number }>>(`/campaigns/${id}/retry-failed`),

  /** Returns the absolute download URL — let the browser handle the file save. */
  exportCsvUrl: (params: { status?: string; channel?: string; viewAll?: boolean } = {}) => {
    const q = new URLSearchParams();
    if (params.status) q.set('status', params.status);
    if (params.channel) q.set('channel', params.channel);
    if (params.viewAll) q.set('viewAll', 'true');
    const qs = q.toString();
    return `/campaigns/export.csv${qs ? '?' + qs : ''}`;
  },


  getReport: (id: string) =>
    axiosInstance.get<any, ApiResponse<CampaignReportDto>>(`/campaigns/${id}/report`),

  getMessages: (id: string, params: { pageNumber?: number; pageSize?: number; status?: string }) =>
    axiosInstance.get<any, PagedResponse<CampaignMessageDto>>(`/campaigns/${id}/messages`, { params }),
};
