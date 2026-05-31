import { useEffect, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel, HttpTransportType } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';

/**
 * Day 8 — real-time inbox push.
 * Opens a SignalR connection to /hubs/inbox (per-user group on the server) and, on
 * "inbox:new" / "inbox:ai-ready" events, invalidates the relevant TanStack queries so the
 * conversation list + open thread + unread badge refresh INSTANTLY (no 30s poll wait).
 *
 * The 30s polling in InboxPage/NotificationBell stays as a safety net if the socket drops.
 */
export function useInboxRealtime(onEvent?: (evt: 'new' | 'ai-ready', threadId: string, inboxMessageId: string) => void) {
  const queryClient = useQueryClient();
  const connRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    // Derive the hub URL from the API base (strip the trailing /api/v1).
    const apiBase = import.meta.env.VITE_API_URL || 'http://localhost:5211/api/v1';
    const origin = apiBase.replace(/\/api\/v1\/?$/, '');
    const hubUrl = `${origin}/hubs/inbox`;

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => localStorage.getItem('accessToken') || '',
        // WebSockets preferred; fall back to SSE/long-polling if blocked.
        transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connRef.current = connection;

    const invalidate = (threadId: string) => {
      queryClient.invalidateQueries({ queryKey: ['inbox-threads'] });
      queryClient.invalidateQueries({ queryKey: ['inbox-thread', threadId] });
      queryClient.invalidateQueries({ queryKey: ['inbox-unread-count'] });
      // Also refresh the bell's notification list.
      queryClient.invalidateQueries({ queryKey: ['notifications'] });
    };

    connection.on('inbox:new', (payload: { threadId: string; inboxMessageId: string }) => {
      invalidate(payload.threadId);
      onEvent?.('new', payload.threadId, payload.inboxMessageId);
    });

    connection.on('inbox:ai-ready', (payload: { threadId: string; inboxMessageId: string }) => {
      invalidate(payload.threadId);
      onEvent?.('ai-ready', payload.threadId, payload.inboxMessageId);
    });

    connection.start().catch((err) => {
      // Non-fatal — polling fallback keeps the UI fresh even if the socket can't connect.
      console.warn('[useInboxRealtime] SignalR connect failed (polling fallback active):', err?.message ?? err);
    });

    return () => {
      connection.stop().catch(() => {});
      connRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return connRef;
}
