import axiosInstance from './axiosInstance';
import type { ApiResponse } from '../types/api.types';
import type { AuthResponseDto } from '../types/contact.types';

export const authApi = {
  register: (data: { fullName: string; email: string; password: string }) =>
    axiosInstance.post<any, ApiResponse<AuthResponseDto>>('/auth/register', data),

  login: (data: { email: string; password: string }) =>
    axiosInstance.post<any, ApiResponse<AuthResponseDto>>('/auth/login', data),

  refresh: (refreshToken: string) =>
    axiosInstance.post<any, ApiResponse<AuthResponseDto>>('/auth/refresh', { refreshToken }),

  logout: (refreshToken: string) =>
    axiosInstance.post('/auth/logout', { refreshToken }),
};
