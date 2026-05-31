import axiosInstance from './axiosInstance';
import type { ApiResponse } from '../types/api.types';

export interface MyProfile {
  id: string;
  fullName: string;
  email: string;
  role: string;
  smtpGroupName?: string | null;
  createdAt: string;
}

export const meApi = {
  getProfile: () =>
    axiosInstance.get<any, ApiResponse<MyProfile>>('/me/profile'),

  updateProfile: (data: { fullName: string }) =>
    axiosInstance.put<any, ApiResponse<MyProfile>>('/me/profile', data),

  changePassword: (data: { currentPassword: string; newPassword: string }) =>
    axiosInstance.post<any, ApiResponse<null>>('/me/password', data),
};
