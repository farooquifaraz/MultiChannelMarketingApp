import axiosInstance from './axiosInstance';
import type { ApiResponse } from '../types/api.types';
import type { DashboardStatsDto, ChannelBreakdownDto, CampaignDto } from '../types/campaign.types';

export const dashboardApi = {
  getStats: () =>
    axiosInstance.get<any, ApiResponse<DashboardStatsDto>>('/dashboard/stats'),

  getRecentCampaigns: (count = 5) =>
    axiosInstance.get<any, ApiResponse<CampaignDto[]>>('/dashboard/recent-campaigns', { params: { count } }),

  getChannelBreakdown: () =>
    axiosInstance.get<any, ApiResponse<ChannelBreakdownDto>>('/dashboard/channel-breakdown'),
};
