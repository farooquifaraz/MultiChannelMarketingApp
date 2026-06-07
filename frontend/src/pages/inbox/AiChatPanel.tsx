import { useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Sparkles, Send, Trash2, FileText, CheckSquare, Loader2, Bot, User as UserIcon } from 'lucide-react';
import toast from 'react-hot-toast';
import { inboxApi } from '../../api/inboxApi';
import type { AiChatTurn, InboxThreadDetail } from '../../types/inbox.types';

const DEFAULT_QUESTIONS = ['Summarize this email', 'What is the sender asking for?', 'List action items / next steps'];

/** Lightweight text → HTML: paragraphs + bullet lines + bold (**x**). Keeps AI answers readable without a markdown lib. */
function formatAnswer(text: string): string {
  if (!text) return '';
  const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const lines = text.split('\n');
  let html = '';
  let inList = false;
  for (const raw of lines) {
    const line = raw.trim();
    const bulletMatch = line.match(/^[-*•]\s+(.*)/);
    if (bulletMatch) {
      if (!inList) { html += '<ul>'; inList = true; }
      html += `<li>${bold(esc(bulletMatch[1]))}</li>`;
    } else {
      if (inList) { html += '</ul>'; inList = false; }
      if (line) html += `<p>${bold(esc(line))}</p>`;
    }
  }
  if (inList) html += '</ul>';
  return html;
  function bold(s: string) { return s.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>'); }
}

export default function AiChatPanel({ thread }: { thread: InboxThreadDetail }) {
  const threadId = thread.threadId;
  const queryClient = useQueryClient();
  const scrollRef = useRef<HTMLDivElement | null>(null);
  const [input, setInput] = useState('');

  const { data: chatResp, isLoading } = useQuery({
    queryKey: ['inbox-chat', threadId],
    queryFn: () => inboxApi.getChat(threadId),
  });
  const history: AiChatTurn[] = (chatResp as any)?.data || [];

  // Dynamic suggestion chips (ChatGPT/Gemini style):
  //   - Before any chat: use the email's pre-generated questions.
  //   - After chatting: use the LATEST assistant turn's follow-up suggestions.
  //   - Always hide questions the user already asked (no repeats).
  const askedLower = new Set(
    history.filter(t => t.role === 'user').map(t => t.content.trim().toLowerCase())
  );
  const lastAssistant = [...history].reverse().find(t => t.role === 'assistant');
  const dynamicSuggestions = (lastAssistant?.suggestions && lastAssistant.suggestions.length > 0)
    ? lastAssistant.suggestions
    : ((thread.aiSuggestedQuestions && thread.aiSuggestedQuestions.length > 0) ? thread.aiSuggestedQuestions : DEFAULT_QUESTIONS);
  const questions = dynamicSuggestions.filter(q => !askedLower.has(q.trim().toLowerCase())).slice(0, 3);

  // Pending optimistic state while awaiting AI answer.
  const [pending, setPending] = useState<{ question: string } | null>(null);

  const askMutation = useMutation({
    mutationFn: (question: string) => inboxApi.askChat(threadId, question),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox-chat', threadId] });
      setPending(null);
    },
    onError: (e: any, question) => {
      // 8-sec toast (429/quota messages are long + actionable). Restore the question so the user can retry.
      toast.error(e?.response?.data?.message || 'AI request failed', { duration: 8000 });
      setPending(null);
      setInput((cur) => cur || question);
    },
  });

  const clearMutation = useMutation({
    mutationFn: () => inboxApi.clearChat(threadId),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ['inbox-chat', threadId] }); toast.success('Chat cleared'); },
  });

  const send = (question: string) => {
    const q = question.trim();
    if (!q || askMutation.isPending) return;
    setPending({ question: q });
    setInput('');
    askMutation.mutate(q);
  };

  // Auto-scroll to bottom on new content.
  useEffect(() => {
    if (scrollRef.current) scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
  }, [history.length, pending]);

  const isEmpty = history.length === 0 && !pending;

  return (
    <div className="flex flex-col h-full" style={{ minHeight: 480 }}>
      {/* Header */}
      <div className="px-5 py-3 border-b border-gray-100 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <div className="w-7 h-7 rounded-lg bg-gradient-to-br from-purple-500 to-primary-500 flex items-center justify-center">
            <Sparkles className="w-4 h-4 text-white" />
          </div>
          <div>
            <p className="text-sm font-semibold text-gray-900">Ask AI about this email</p>
            <p className="text-[11px] text-gray-400">Summaries, action items, advice — not sent to the recipient</p>
          </div>
        </div>
        {history.length > 0 && (
          <button onClick={() => clearMutation.mutate()} className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg" title="Clear chat">
            <Trash2 className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Chat area */}
      <div ref={scrollRef} className="flex-1 overflow-y-auto px-4 py-4 space-y-3 bg-gray-50/40">
        {isLoading ? (
          <div className="flex items-center justify-center py-10"><Loader2 className="w-6 h-6 text-purple-500 animate-spin" /></div>
        ) : isEmpty ? (
          <div className="text-center py-6">
            <Bot className="w-12 h-12 text-purple-200 mx-auto mb-2" />
            <p className="text-sm text-gray-500">Ask anything about this conversation</p>
            <p className="text-xs text-gray-400 mt-1">Try a suggestion below, or type your own question.</p>
          </div>
        ) : (
          <>
            {history.map((t) => <ChatBubble key={t.id} turn={t} />)}
            {pending && (
              <>
                <ChatBubble turn={{ id: 'pending-q', role: 'user', content: pending.question, createdAt: new Date().toISOString() }} />
                <div className="flex justify-start">
                  <div className="max-w-[85%] rounded-2xl bg-white border border-gray-200 px-4 py-3 flex items-center gap-2">
                    <Loader2 className="w-4 h-4 text-purple-500 animate-spin" />
                    <span className="text-sm text-gray-500">Thinking…</span>
                  </div>
                </div>
              </>
            )}
          </>
        )}
      </div>

      {/* Suggested questions + quick actions */}
      <div className="px-4 pt-3 border-t border-gray-100">
        {questions.length > 0 && (
          <div className="flex flex-wrap gap-1.5 mb-2">
            {history.length > 0 && <span className="w-full text-[10px] text-gray-400 uppercase tracking-wide mb-0.5">Suggested follow-ups</span>}
            {questions.map((q, i) => (
              <button key={i} onClick={() => send(q)} disabled={askMutation.isPending}
                className="px-3 py-1.5 bg-purple-50 text-purple-700 text-xs font-medium rounded-full hover:bg-purple-100 disabled:opacity-50 transition-colors border border-purple-100">
                {q}
              </button>
            ))}
          </div>
        )}
        <div className="flex gap-1.5 mb-2">
          <button onClick={() => send('Summarize this email conversation concisely.')} disabled={askMutation.isPending}
            className="flex items-center gap-1 px-2.5 py-1 text-[11px] font-medium text-gray-600 border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-50">
            <FileText className="w-3.5 h-3.5" /> Summarize
          </button>
          <button onClick={() => send('List the action items and next steps from this conversation as a checklist.')} disabled={askMutation.isPending}
            className="flex items-center gap-1 px-2.5 py-1 text-[11px] font-medium text-gray-600 border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-50">
            <CheckSquare className="w-3.5 h-3.5" /> Action items
          </button>
        </div>
      </div>

      {/* Input */}
      <div className="px-4 pb-4">
        <div className="flex items-end gap-2">
          <textarea
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(input); } }}
            placeholder="Ask about this email…  (Enter to send, Shift+Enter for newline)"
            rows={2}
            className="flex-1 px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-purple-500 outline-none resize-none"
          />
          <button onClick={() => send(input)} disabled={askMutation.isPending || !input.trim()}
            className="p-2.5 bg-gradient-to-r from-purple-600 to-primary-600 text-white rounded-lg hover:shadow-lg disabled:opacity-50">
            <Send className="w-4 h-4" />
          </button>
        </div>
      </div>
    </div>
  );
}

function ChatBubble({ turn }: { turn: AiChatTurn }) {
  const isUser = turn.role === 'user';
  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'}`}>
      <div className={`max-w-[85%] rounded-2xl px-4 py-2.5 ${
        isUser ? 'bg-primary-600 text-white' : 'bg-white border border-gray-200 text-gray-800'
      }`}>
        <div className="flex items-center gap-1.5 mb-1">
          {isUser ? <UserIcon className="w-3 h-3 opacity-70" /> : <Sparkles className="w-3 h-3 text-purple-500" />}
          <span className={`text-[10px] font-semibold uppercase tracking-wide ${isUser ? 'text-primary-100' : 'text-purple-600'}`}>
            {isUser ? 'You' : 'AI'}
          </span>
          {!isUser && turn.providerUsed && <span className="text-[10px] text-gray-400">· {turn.providerUsed}</span>}
        </div>
        {isUser ? (
          <p className="text-sm whitespace-pre-wrap">{turn.content}</p>
        ) : (
          <div className="text-sm ai-chat-answer" dangerouslySetInnerHTML={{ __html: formatAnswer(turn.content) }} />
        )}
      </div>
    </div>
  );
}
