import { useEffect, useRef, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Inbox, Mail, MailOpen, RefreshCw, Sparkles, Send, Save, RotateCcw, Eye, AlertCircle,
  ChevronLeft, ChevronRight, Search, X, Bold, Italic, Underline as UnderlineIcon, Link as LinkIcon, List, ListOrdered, Wifi,
  Trash2, ChevronDown, ChevronUp
} from 'lucide-react';
import toast from 'react-hot-toast';
import { inboxApi } from '../../api/inboxApi';
import type { InboxThreadDetail, InboxThreadListItem, ThreadMessage } from '../../types/inbox.types';
import { useInboxRealtime } from '../../hooks/useInboxRealtime';
import SafeHtml from '../../components/SafeHtml';
import AiChatPanel from './AiChatPanel';

const categoryColor = (cat?: string | null): string => {
  switch ((cat || '').toLowerCase()) {
    case 'interested': return 'bg-green-100 text-green-700';
    case 'question': return 'bg-blue-100 text-blue-700';
    case 'complaint': return 'bg-red-100 text-red-700';
    case 'unsubscribe': return 'bg-amber-100 text-amber-700';
    case 'spam': return 'bg-gray-100 text-gray-600';
    default: return 'bg-purple-50 text-purple-700';
  }
};

function timeAgo(iso?: string | null): string {
  if (!iso) return '';
  const seconds = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000));
  if (seconds < 60) return 'Just now';
  const m = Math.floor(seconds / 60); if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60); if (h < 24) return `${h}h ago`;
  const d = Math.floor(h / 24); return `${d}d ago`;
}

function initials(name?: string | null, email?: string): string {
  const src = (name || email || '?').trim();
  return src.charAt(0).toUpperCase();
}

export default function InboxPage() {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();

  const [pageNumber, setPageNumber] = useState(1);
  const pageSize = 25;
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [category, setCategory] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  // Real-time: SignalR pushes invalidate the queries below instantly.
  useInboxRealtime();

  useEffect(() => {
    const t = setTimeout(() => { setDebouncedSearch(searchInput); setPageNumber(1); }, 400);
    return () => clearTimeout(t);
  }, [searchInput]);

  const params = {
    pageNumber, pageSize,
    unreadOnly: unreadOnly || undefined,
    category: category || undefined,
    search: debouncedSearch || undefined,
  };

  const { data: listResp, isLoading: listLoading, refetch: refetchList } = useQuery({
    queryKey: ['inbox-threads', params],
    queryFn: () => inboxApi.listThreads(params),
    refetchInterval: 30000, // safety-net poll; SignalR is primary
  });

  const threads: InboxThreadListItem[] = (listResp as any)?.data || [];
  const totalCount = (listResp as any)?.totalCount || 0;
  const totalPages = (listResp as any)?.totalPages || 1;

  const selectedThreadId = searchParams.get('thread');
  const { data: threadResp, refetch: refetchThread } = useQuery({
    queryKey: ['inbox-thread', selectedThreadId],
    queryFn: () => inboxApi.getThread(selectedThreadId!),
    enabled: !!selectedThreadId,
  });
  const thread: InboxThreadDetail | undefined = (threadResp as any)?.data;

  const selectThread = (id: string) => {
    const sp = new URLSearchParams(searchParams);
    sp.set('thread', id);
    setSearchParams(sp, { replace: true });
  };

  const markAllReadMutation = useMutation({
    mutationFn: () => inboxApi.markAllRead(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox-threads'] });
      queryClient.invalidateQueries({ queryKey: ['inbox-unread-count'] });
      toast.success('All marked as read');
    },
  });

  const clearAllMutation = useMutation({
    mutationFn: (resetCursor: boolean) => inboxApi.clearAll(resetCursor),
    onSuccess: (res: any) => {
      queryClient.invalidateQueries({ queryKey: ['inbox-threads'] });
      queryClient.invalidateQueries({ queryKey: ['inbox-unread-count'] });
      const sp = new URLSearchParams(searchParams); sp.delete('thread'); setSearchParams(sp, { replace: true });
      const n = res?.data?.deleted ?? 0;
      toast.success(`Cleared ${n} message(s). ${res?.data?.resetImapCursor ? 'Emails will re-sync on next poll.' : ''}`);
    },
    onError: (e: any) => toast.error(e?.response?.data?.message || 'Clear failed'),
  });

  const deleteThreadMutation = useMutation({
    mutationFn: (threadId: string) => inboxApi.deleteThread(threadId),
    onSuccess: (_res, threadId) => {
      queryClient.invalidateQueries({ queryKey: ['inbox-threads'] });
      queryClient.invalidateQueries({ queryKey: ['inbox-unread-count'] });
      if (selectedThreadId === threadId) {
        const sp = new URLSearchParams(searchParams); sp.delete('thread'); setSearchParams(sp, { replace: true });
      }
      toast.success('Conversation deleted');
    },
    onError: (e: any) => toast.error(e?.response?.data?.message || 'Delete failed'),
  });

  const handleClearAll = () => {
    const resetCursor = window.confirm(
      'Clear your ENTIRE inbox?\n\nOK = also re-sync the SAME emails on the next poll (re-test from scratch).\nCancel = keep them deleted (won\'t re-appear).\n\nClose this dialog to abort.'
    );
    // confirm() only gives OK/Cancel — use a second confirm to allow true abort.
    if (!window.confirm(`This will permanently delete all your inbox conversations.${resetCursor ? ' Emails WILL re-sync on next poll.' : ' Emails will NOT re-appear.'}\n\nProceed?`)) return;
    clearAllMutation.mutate(resetCursor);
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-indigo-100 rounded-xl"><Inbox className="w-6 h-6 text-indigo-600" /></div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
              Inbox
              <span className="flex items-center gap-1 text-[11px] font-medium text-green-600" title="Live updates active">
                <Wifi className="w-3.5 h-3.5" /> Live
              </span>
            </h1>
            <p className="text-sm text-gray-500">{totalCount.toLocaleString()} {totalCount === 1 ? 'conversation' : 'conversations'}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => markAllReadMutation.mutate()}
            disabled={markAllReadMutation.isPending}
            className="flex items-center gap-2 px-3 py-2 border border-gray-200 rounded-xl text-sm font-medium hover:bg-gray-50 disabled:opacity-50"
          >
            <MailOpen className="w-4 h-4" /> Mark all read
          </button>
          <button onClick={() => refetchList()} className="flex items-center gap-2 px-3 py-2 border border-gray-200 rounded-xl text-sm font-medium hover:bg-gray-50">
            <RefreshCw className="w-4 h-4" /> Refresh
          </button>
          <button
            onClick={handleClearAll}
            disabled={clearAllMutation.isPending || threads.length === 0}
            className="flex items-center gap-2 px-3 py-2 border border-red-200 text-red-600 rounded-xl text-sm font-medium hover:bg-red-50 disabled:opacity-50"
            title="Delete all conversations (option to re-sync for testing)"
          >
            <Trash2 className="w-4 h-4" /> Clear inbox
          </button>
        </div>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-2xl border border-gray-100 p-3 flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input type="text" value={searchInput} onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Search subject, sender, AI summary…"
            className="w-full pl-9 pr-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-primary-500 outline-none" />
        </div>
        <label className="flex items-center gap-2 px-3 py-2 border border-gray-200 rounded-lg cursor-pointer text-sm">
          <input type="checkbox" checked={unreadOnly} onChange={(e) => { setUnreadOnly(e.target.checked); setPageNumber(1); }} className="w-4 h-4" />
          Unread only
        </label>
        <select value={category} onChange={(e) => { setCategory(e.target.value); setPageNumber(1); }}
          className="px-3 py-2 border border-gray-200 rounded-lg text-sm">
          <option value="">All categories</option>
          <option value="question">Question</option>
          <option value="interested">Interested</option>
          <option value="complaint">Complaint</option>
          <option value="unsubscribe">Unsubscribe</option>
          <option value="spam">Spam</option>
          <option value="other">Other</option>
        </select>
      </div>

      {/* 2-column conversation layout */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-4">
        {/* Left: conversation list */}
        <div className="lg:col-span-5 bg-white rounded-2xl border border-gray-100 overflow-hidden flex flex-col" style={{ minHeight: 620 }}>
          {listLoading ? (
            <div className="flex-1 flex items-center justify-center py-12">
              <div className="w-8 h-8 border-4 border-primary-500 border-t-transparent rounded-full animate-spin" />
            </div>
          ) : threads.length === 0 ? (
            <div className="flex-1 flex flex-col items-center justify-center py-16 text-center">
              <Inbox className="w-14 h-14 text-gray-200 mb-3" />
              <p className="text-gray-500 text-sm">No conversations yet</p>
              <p className="text-xs text-gray-400 mt-1">Replies to your campaigns appear here as threads.</p>
            </div>
          ) : (
            <div className="flex-1 overflow-y-auto divide-y divide-gray-50">
              {threads.map((t) => (
                <div key={t.threadId}
                  className={`group relative w-full px-4 py-3 hover:bg-gray-50/70 transition-colors cursor-pointer ${
                    selectedThreadId === t.threadId ? 'bg-indigo-50/60 border-l-4 border-indigo-500' : 'border-l-4 border-transparent'
                  }`}
                  onClick={() => selectThread(t.threadId)}>
                  <div className="flex items-start gap-3">
                    <div className="w-9 h-9 rounded-full bg-gradient-to-br from-indigo-400 to-purple-500 text-white flex items-center justify-center text-sm font-bold flex-shrink-0">
                      {initials(t.participantName, t.participantEmail)}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2 mb-0.5">
                        <p className={`text-sm truncate ${t.unreadCount > 0 ? 'font-semibold text-gray-900' : 'text-gray-700'}`}>
                          {t.participantName || t.participantEmail}
                        </p>
                        {t.messageCount > 1 && (
                          <span className="text-[10px] px-1.5 py-0.5 bg-gray-100 text-gray-500 rounded-full">{t.messageCount}</span>
                        )}
                        {t.unreadCount > 0 && <span className="w-2 h-2 bg-blue-500 rounded-full flex-shrink-0" />}
                        {t.aiCategory && (
                          <span className={`px-1.5 py-0.5 rounded text-[10px] font-semibold uppercase tracking-wide ${categoryColor(t.aiCategory)}`}>
                            {t.aiCategory}
                          </span>
                        )}
                      </div>
                      <p className={`text-sm truncate ${t.unreadCount > 0 ? 'text-gray-900' : 'text-gray-600'}`}>{t.subject}</p>
                      {t.latestPreview && <p className="text-xs text-gray-500 mt-1 line-clamp-1">{t.latestPreview}</p>}
                      <div className="flex items-center gap-2 mt-1">
                        {t.matchedCampaignName && (
                          <span className="text-[11px] text-gray-500 truncate">↳ {t.matchedCampaignName}</span>
                        )}
                        {t.isOrphan && <span className="text-[11px] text-amber-600">⚠ orphan</span>}
                      </div>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <span className="text-xs text-gray-400 whitespace-nowrap">{timeAgo(t.lastActivityAt)}</span>
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          if (window.confirm(`Delete this conversation with ${t.participantName || t.participantEmail}? This cannot be undone.`))
                            deleteThreadMutation.mutate(t.threadId);
                        }}
                        className="p-1 text-gray-300 hover:text-red-600 hover:bg-red-50 rounded opacity-0 group-hover:opacity-100 transition-opacity"
                        title="Delete conversation"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {totalPages > 1 && (
            <div className="px-3 py-2 border-t border-gray-100 flex items-center justify-between">
              <p className="text-xs text-gray-500">Page {pageNumber} / {totalPages}</p>
              <div className="flex items-center gap-1">
                <button onClick={() => setPageNumber((p) => Math.max(1, p - 1))} disabled={pageNumber <= 1}
                  className="p-1.5 rounded hover:bg-gray-100 disabled:opacity-40"><ChevronLeft className="w-4 h-4" /></button>
                <button onClick={() => setPageNumber((p) => p + 1)} disabled={pageNumber >= totalPages}
                  className="p-1.5 rounded hover:bg-gray-100 disabled:opacity-40"><ChevronRight className="w-4 h-4" /></button>
              </div>
            </div>
          )}
        </div>

        {/* Right: thread timeline + composer */}
        <div className="lg:col-span-7 bg-white rounded-2xl border border-gray-100" style={{ minHeight: 620 }}>
          {thread ? (
            <ThreadView thread={thread} onReplied={() => { refetchThread(); refetchList(); }} onAiRegenerated={() => refetchThread()} />
          ) : (
            <div className="flex flex-col items-center justify-center h-full py-20 text-center px-6">
              <Mail className="w-14 h-14 text-gray-200 mb-3" />
              <p className="text-gray-500 text-sm">Select a conversation</p>
              <p className="text-xs text-gray-400 mt-1 max-w-sm">See the full back-and-forth in one thread. AI drafts a reply for the latest message — edit and send.</p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ============================ THREAD VIEW ============================

function ThreadView({ thread, onReplied, onAiRegenerated }: {
  thread: InboxThreadDetail;
  onReplied: () => void;
  onAiRegenerated: () => void;
}) {
  const navigate = useNavigate();
  const editorRef = useRef<HTMLDivElement | null>(null);
  const timelineRef = useRef<HTMLDivElement | null>(null);
  const [subject, setSubject] = useState('');
  const [currentHtml, setCurrentHtml] = useState('');
  const [showPreview, setShowPreview] = useState(false);
  const [sending, setSending] = useState(false);
  const [tab, setTab] = useState<'conversation' | 'ai'>('conversation');

  // Reset to Conversation tab when switching threads.
  useEffect(() => { setTab('conversation'); }, [thread.threadId]);

  const latestId = thread.latestInboundMessageId;

  // Load AI draft (or user-edited) into the editor when the thread changes.
  useEffect(() => {
    const base = thread.userEditedReply || thread.aiSuggestedReply || '';
    setCurrentHtml(base);
    if (editorRef.current) editorRef.current.innerHTML = base;
    setSubject((thread.subject || '').startsWith('Re:') ? thread.subject : `Re: ${thread.subject}`);
    // scroll timeline to bottom (latest message)
    setTimeout(() => { if (timelineRef.current) timelineRef.current.scrollTop = timelineRef.current.scrollHeight; }, 50);
  }, [thread.threadId, thread.aiSuggestedReply, thread.userEditedReply, thread.subject]);

  const aiOriginal = thread.aiSuggestedReply || '';
  const isEdited = currentHtml.trim() !== aiOriginal.trim() && !!aiOriginal;

  // Auto-save draft (debounced) — targets the latest inbound message.
  useEffect(() => {
    if (!latestId || !currentHtml || currentHtml === thread.userEditedReply || currentHtml === thread.aiSuggestedReply) return;
    const t = setTimeout(() => { inboxApi.saveDraft(latestId, currentHtml).catch(() => {}); }, 2000);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentHtml, latestId]);

  const onEditorInput = () => { if (editorRef.current) setCurrentHtml(editorRef.current.innerHTML); };
  const applyCommand = (command: string, value?: string) => {
    document.execCommand(command, false, value);
    if (editorRef.current) { editorRef.current.focus(); setCurrentHtml(editorRef.current.innerHTML); }
  };
  const insertLink = () => { const url = prompt('Enter URL:'); if (url) applyCommand('createLink', url); };
  const resetToAi = () => { setCurrentHtml(aiOriginal); if (editorRef.current) editorRef.current.innerHTML = aiOriginal; };

  const regenerate = async () => {
    if (!latestId) return;
    try {
      const res: any = await inboxApi.regenerateAi(latestId);
      const fresh = res?.data;
      if (fresh?.aiSuggestedReply) {
        setCurrentHtml(fresh.aiSuggestedReply);
        if (editorRef.current) editorRef.current.innerHTML = fresh.aiSuggestedReply;
        toast.success('AI suggestion regenerated');
      } else if (fresh?.aiGenerationError) {
        toast.error(fresh.aiGenerationError, { duration: 8000 });
      } else {
        toast.error('AI returned no suggestion — check Settings → AI Assistant.', { duration: 8000 });
      }
      onAiRegenerated();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Regenerate failed', { duration: 8000 });
    }
  };

  const sendReply = async () => {
    if (!latestId) return;
    if (!subject.trim()) { toast.error('Subject is required'); return; }
    if (!currentHtml.trim()) { toast.error('Reply body is empty'); return; }
    setSending(true);
    try {
      await inboxApi.sendReply(latestId, { subject, html: currentHtml });
      toast.success('Reply sent — threaded in recipient\'s inbox.');
      onReplied();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Send failed');
    } finally { setSending(false); }
  };

  return (
    <div className="flex flex-col h-full">
      {/* Thread header */}
      <div className="px-5 py-3 border-b border-gray-100 flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-base font-semibold text-gray-900 truncate">{thread.subject}</p>
          <div className="flex flex-wrap items-center gap-2 mt-1">
            <span className="text-xs text-gray-500">{thread.participantName ? `${thread.participantName} <${thread.participantEmail}>` : thread.participantEmail}</span>
            <span className="text-xs text-gray-400">· {thread.messages.length} messages</span>
            {thread.smtpGroupName && <span className="text-xs text-gray-400">· via {thread.smtpGroupName}</span>}
            {thread.matchedCampaignName && (
              <button onClick={() => thread.matchedCampaignId && navigate(`/campaigns/${thread.matchedCampaignId}`)}
                className="text-xs px-2 py-0.5 bg-indigo-50 text-indigo-700 rounded-full hover:bg-indigo-100">
                ↳ {thread.matchedCampaignName}
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Tab bar: Conversation | Ask AI */}
      <div className="flex border-b border-gray-100 px-3">
        <button onClick={() => setTab('conversation')}
          className={`px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors ${
            tab === 'conversation' ? 'border-indigo-600 text-indigo-700' : 'border-transparent text-gray-500 hover:text-gray-700'
          }`}>
          Conversation
        </button>
        <button onClick={() => setTab('ai')}
          className={`px-4 py-2.5 text-sm font-medium border-b-2 -mb-px transition-colors flex items-center gap-1.5 ${
            tab === 'ai' ? 'border-purple-600 text-purple-700' : `border-transparent text-gray-500 hover:text-gray-700 ask-ai-blink`
          }`}>
          <Sparkles className="w-3.5 h-3.5 ai-spark text-purple-500" /> Ask AI
        </button>
      </div>

      {tab === 'ai' ? (
        <AiChatPanel thread={thread} />
      ) : (
      <>
      {/* Timeline — Gmail-style: only the latest message expanded, older ones collapsed */}
      <div ref={timelineRef} className="flex-1 overflow-y-auto px-5 py-4 space-y-2.5 bg-gray-50/40" style={{ maxHeight: 380 }}>
        {thread.messages.map((m, idx) => (
          <MessageBubble
            key={`${m.direction}-${m.id}`}
            m={m}
            participant={thread.participantName || thread.participantEmail}
            defaultExpanded={idx === thread.messages.length - 1}
          />
        ))}
      </div>

      {/* AI summary */}
      {thread.aiSummary && (
        <div className="mx-5 mb-3 p-3 bg-gradient-to-r from-purple-50 to-indigo-50 border border-purple-100 rounded-xl">
          <div className="flex items-start gap-2">
            <Sparkles className="w-4 h-4 text-purple-600 mt-0.5 flex-shrink-0" />
            <div className="flex-1">
              <div className="flex items-center gap-2 mb-1">
                <p className="text-xs font-semibold text-purple-900 uppercase tracking-wide">AI Summary</p>
                {thread.aiCategory && <span className={`px-1.5 py-0.5 rounded text-[10px] font-semibold uppercase ${categoryColor(thread.aiCategory)}`}>{thread.aiCategory}</span>}
                {thread.aiProviderUsed && <span className="text-[10px] text-purple-600">via {thread.aiProviderUsed}</span>}
              </div>
              <p className="text-sm text-gray-800">{thread.aiSummary}</p>
            </div>
          </div>
        </div>
      )}

      {thread.aiGenerationError && (
        <div className="mx-5 mb-3 p-3 bg-red-50 border border-red-100 rounded-xl flex items-start gap-2">
          <AlertCircle className="w-4 h-4 text-red-600 mt-0.5 flex-shrink-0" />
          <div className="flex-1">
            <p className="text-xs font-semibold text-red-900">AI generation failed</p>
            <p className="text-xs text-red-700 mt-1">{thread.aiGenerationError}</p>
            <button onClick={regenerate} className="mt-2 text-xs text-red-700 hover:text-red-900 underline font-medium">Retry</button>
          </div>
        </div>
      )}

      {/* Composer */}
      <div className="px-5 pb-4 border-t border-gray-100 pt-3">
        <div className="flex items-center justify-between mb-2">
          <p className="text-sm font-semibold text-gray-900">Reply</p>
          <div className="flex items-center gap-1.5">
            {isEdited && (
              <span className="flex items-center gap-1 text-[11px] text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full font-medium">
                <span className="w-1.5 h-1.5 bg-amber-500 rounded-full" /> Edited
              </span>
            )}
            {thread.aiInputTokens && thread.aiOutputTokens && (
              <span className="text-[11px] text-gray-400">{thread.aiInputTokens}+{thread.aiOutputTokens} tok</span>
            )}
          </div>
        </div>

        <input type="text" value={subject} onChange={(e) => setSubject(e.target.value)} placeholder="Subject"
          className="w-full px-3 py-2 mb-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-primary-500 outline-none" />

        <div className="flex items-center gap-0.5 px-2 py-1.5 border border-b-0 border-gray-200 rounded-t-lg bg-gray-50">
          <button onClick={() => applyCommand('bold')} className="p-1.5 hover:bg-white rounded" title="Bold"><Bold className="w-3.5 h-3.5" /></button>
          <button onClick={() => applyCommand('italic')} className="p-1.5 hover:bg-white rounded" title="Italic"><Italic className="w-3.5 h-3.5" /></button>
          <button onClick={() => applyCommand('underline')} className="p-1.5 hover:bg-white rounded" title="Underline"><UnderlineIcon className="w-3.5 h-3.5" /></button>
          <span className="w-px h-4 bg-gray-300 mx-1" />
          <button onClick={() => applyCommand('insertUnorderedList')} className="p-1.5 hover:bg-white rounded" title="Bullet list"><List className="w-3.5 h-3.5" /></button>
          <button onClick={() => applyCommand('insertOrderedList')} className="p-1.5 hover:bg-white rounded" title="Numbered list"><ListOrdered className="w-3.5 h-3.5" /></button>
          <button onClick={insertLink} className="p-1.5 hover:bg-white rounded" title="Insert link"><LinkIcon className="w-3.5 h-3.5" /></button>
        </div>

        <div ref={editorRef} contentEditable suppressContentEditableWarning onInput={onEditorInput}
          className="min-h-[120px] max-h-[200px] overflow-auto px-4 py-3 border border-gray-200 rounded-b-lg text-sm focus:ring-2 focus:ring-primary-500 focus:border-transparent outline-none bg-white"
          style={{ whiteSpace: 'pre-wrap' }} />

        <div className="flex flex-wrap items-center justify-between gap-2 mt-3">
          <div className="flex items-center gap-2">
            <button onClick={regenerate} className="flex items-center gap-1.5 px-3 py-1.5 border border-indigo-200 text-indigo-700 rounded-lg text-xs font-medium hover:bg-indigo-50">
              <Sparkles className="w-3.5 h-3.5" /> Regenerate
            </button>
            {aiOriginal && (
              <button onClick={resetToAi} disabled={!isEdited}
                className="flex items-center gap-1.5 px-3 py-1.5 border border-gray-200 rounded-lg text-xs font-medium hover:bg-gray-50 disabled:opacity-40">
                <RotateCcw className="w-3.5 h-3.5" /> Reset to AI
              </button>
            )}
            <button onClick={() => setShowPreview(true)} className="flex items-center gap-1.5 px-3 py-1.5 border border-gray-200 rounded-lg text-xs font-medium hover:bg-gray-50">
              <Eye className="w-3.5 h-3.5" /> Preview
            </button>
            <button onClick={() => latestId && inboxApi.saveDraft(latestId, currentHtml).then(() => toast.success('Draft saved'))}
              className="flex items-center gap-1.5 px-3 py-1.5 text-gray-600 text-xs font-medium hover:bg-gray-50 rounded-lg">
              <Save className="w-3.5 h-3.5" /> Save draft
            </button>
          </div>
          <button onClick={sendReply} disabled={sending}
            className="flex items-center gap-2 px-5 py-2 bg-gradient-to-r from-indigo-600 to-purple-600 text-white rounded-lg text-sm font-medium hover:shadow-lg disabled:opacity-50">
            <Send className="w-4 h-4" />{sending ? 'Sending…' : 'Send Reply'}
          </button>
        </div>
      </div>
      </>
      )}

      {showPreview && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-sm z-50 flex items-center justify-center p-4" onClick={() => setShowPreview(false)}>
          <div className="bg-white rounded-2xl shadow-2xl max-w-2xl w-full max-h-[80vh] overflow-hidden" onClick={(e) => e.stopPropagation()}>
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h3 className="text-base font-semibold">Preview reply</h3>
              <button onClick={() => setShowPreview(false)} className="p-1 hover:bg-gray-100 rounded"><X className="w-4 h-4" /></button>
            </div>
            <div className="p-5">
              <p className="text-xs text-gray-500 mb-1">To: <span className="text-gray-800">{thread.participantEmail}</span></p>
              <p className="text-xs text-gray-500 mb-3">Subject: <span className="text-gray-800 font-medium">{subject}</span></p>
              <div className="border border-gray-200 rounded-lg p-4 max-h-[400px] overflow-auto bg-white">
                <SafeHtml html={currentHtml} />
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function MessageBubble({ m, participant, defaultExpanded }: { m: ThreadMessage; participant: string; defaultExpanded: boolean }) {
  const isOut = m.direction === 'out';
  const [expanded, setExpanded] = useState(defaultExpanded);

  // Collapsed preview text (strip tags for a one-liner).
  const previewText = (() => {
    const src = m.textBody || (m.htmlBody ? m.htmlBody.replace(/<[^>]+>/g, ' ') : '');
    const t = src.replace(/\s+/g, ' ').trim();
    return t.length > 90 ? t.slice(0, 90) + '…' : t;
  })();

  return (
    <div className={`rounded-xl border ${isOut ? 'bg-indigo-50/40 border-indigo-100' : 'bg-white border-gray-200'} shadow-sm`}>
      {/* Header — always visible, click to collapse/expand */}
      <button onClick={() => setExpanded(!expanded)} className="w-full px-4 py-2.5 flex items-center gap-3 text-left">
        <div className={`w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold flex-shrink-0 ${
          isOut ? 'bg-indigo-500 text-white' : 'bg-gradient-to-br from-gray-400 to-gray-500 text-white'
        }`}>
          {isOut ? 'Y' : (participant.charAt(0).toUpperCase())}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-gray-800">{isOut ? 'You' : participant}</span>
            {isOut && <span className="text-[10px] px-1.5 py-0.5 bg-indigo-100 text-indigo-700 rounded-full font-medium">SENT</span>}
          </div>
          {!expanded && previewText && <p className="text-xs text-gray-500 truncate mt-0.5">{previewText}</p>}
        </div>
        <span className="text-[11px] text-gray-400 whitespace-nowrap">{timeAgo(m.at)}</span>
        {expanded ? <ChevronUp className="w-4 h-4 text-gray-300" /> : <ChevronDown className="w-4 h-4 text-gray-300" />}
      </button>

      {/* Body — inline sanitized HTML (no more cramped iframe) */}
      {expanded && (
        <div className="px-4 pb-4 pt-1 border-t border-gray-100/70">
          {m.htmlBody
            ? <SafeHtml html={m.htmlBody} />
            : <p className="whitespace-pre-wrap text-sm text-gray-700 leading-relaxed">{m.textBody || '(empty message)'}</p>}
        </div>
      )}
    </div>
  );
}
