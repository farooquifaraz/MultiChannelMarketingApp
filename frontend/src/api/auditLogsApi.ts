import axiosInstance from './axiosInstance';
import type { ApiResponse, PagedResponse } from '../types/api.types';

export interface AuditLog {
  id: string;
  userId?: string | null;
  userFullName?: string | null;
  userEmail?: string | null;
  action: string;
  entity?: string | null;
  entityId?: string | null;
  details?: string | null;
  ipAddress?: string | null;
  createdAt: string;
}

export interface AuditLogFilters {
  pageNumber?: number;
  pageSize?: number;
  userId?: string;
  action?: string;
  entity?: string;
  fromDate?: string;
  toDate?: string;
  search?: string;
}

export const auditLogsApi = {
  list: (params: AuditLogFilters) =>
    axiosInstance.get<any, PagedResponse<AuditLog>>('/admin/audit-logs', { params }),

  filters: () =>
    axiosInstance.get<any, ApiResponse<{ actions: string[]; entities: string[] }>>('/admin/audit-logs/filters'),

  exportCsvUrl: (params: AuditLogFilters) => {
    const q = new URLSearchParams();
    if (params.userId) q.set('userId', params.userId);
    if (params.action) q.set('action', params.action);
    if (params.entity) q.set('entity', params.entity);
    if (params.fromDate) q.set('fromDate', params.fromDate);
    if (params.toDate) q.set('toDate', params.toDate);
    if (params.search) q.set('search', params.search);
    const qs = q.toString();
    return `/admin/audit-logs/export.csv${qs ? '?' + qs : ''}`;
  },
};
