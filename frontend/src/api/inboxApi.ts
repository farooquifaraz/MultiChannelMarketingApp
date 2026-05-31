import axiosInstance from './axiosInstance';
import type { ApiResponse, PagedResponse } from '../types/api.types';
import type { InboxMessageDetail, InboxMessageListItem, InboxUnreadCount, InboxThreadListItem, InboxThreadDetail, AiChatTurn } from '../types/inbox.types';

export interface InboxListParams {
  pageNumber?: number;
  pageSize?: number;
  viewAll?: boolean;
  unreadOnly?: boolean;
  category?: string;
  smtpGroupId?: string;
  search?: string;
}

export const inboxApi = {
  list: (params: InboxListParams) =>
    axiosInstance.get<any, PagedResponse<InboxMessageListItem>>('/inbox', { params }),

  getById: (id: string) =>
    axiosInstance.get<any, ApiResponse<InboxMessageDetail>>(`/inbox/${id}`),

  unreadCount: () =>
    axiosInstance.get<any, ApiResponse<InboxUnreadCount>>('/inbox/unread-count'),

  markRead: (id: string) =>
    axiosInstance.put<any, ApiResponse<null>>(`/inbox/${id}/read`),

  markAllRead: () =>
    axiosInstance.put<any, ApiResponse<null>>('/inbox/read-all'),

  archive: (id: string) =>
    axiosInstance.put<any, ApiResponse<null>>(`/inbox/${id}/archive`),

  saveDraft: (id: string, html: string) =>
    axiosInstance.put<any, ApiResponse<null>>(`/inbox/${id}/draft`, { html }),

  regenerateAi: (id: string) =>
    axiosInstance.post<any, ApiResponse<InboxMessageDetail>>(`/inbox/${id}/regenerate-ai`),

  sendReply: (id: string, data: { subject: string; html: string }) =>
    axiosInstance.post<any, ApiResponse<null>>(`/inbox/${id}/send-reply`, data),

  // === Day 8: conversation (thread) endpoints ===
  listThreads: (params: { pageNumber?: number; pageSize?: number; viewAll?: boolean; unreadOnly?: boolean; category?: string; search?: string }) =>
    axiosInstance.get<any, PagedResponse<InboxThreadListItem>>('/inbox/threads', { params }),

  getThread: (threadId: string) =>
    axiosInstance.get<any, ApiResponse<InboxThreadDetail>>(`/inbox/threads/${threadId}`),

  deleteThread: (threadId: string) =>
    axiosInstance.delete<any, ApiResponse<null>>(`/inbox/threads/${threadId}`),

  clearAll: (resetImapCursor: boolean) =>
    axiosInstance.post<any, ApiResponse<{ deleted: number; resetImapCursor: boolean }>>(
      `/inbox/clear?resetImapCursor=${resetImapCursor}`),

  // Day 9: Ask-AI chat
  getChat: (threadId: string) =>
    axiosInstance.get<any, ApiResponse<AiChatTurn[]>>(`/inbox/threads/${threadId}/chat`),

  askChat: (threadId: string, question: string) =>
    axiosInstance.post<any, ApiResponse<AiChatTurn>>(`/inbox/threads/${threadId}/chat`, { question }),

  clearChat: (threadId: string) =>
    axiosInstance.delete<any, ApiResponse<null>>(`/inbox/threads/${threadId}/chat`),
};
