export interface InboxMessageListItem {
  id: string;
  fromEmail: string;
  fromName?: string | null;
  subject: string;
  preview?: string | null;
  receivedAt: string;
  isRead: boolean;
  isArchived: boolean;
  isOrphanReply: boolean;
  aiCategory?: string | null;
  aiSummary?: string | null;
  hasAiSuggestion: boolean;
  repliedAt?: string | null;
  matchedCampaignId?: string | null;
  matchedCampaignName?: string | null;
  smtpGroupName?: string | null;
}

export interface InboxMessageDetail extends InboxMessageListItem {
  htmlBody?: string | null;
  textBody?: string | null;
  inReplyToMessageId?: string | null;
  referencesHeader?: string | null;
  toEmail: string;
  matchedCampaignMessageId?: string | null;
  matchedContactId?: string | null;
  matchedContactName?: string | null;
  aiSuggestedReply?: string | null;
  userEditedReply?: string | null;
  aiGeneratedAt?: string | null;
  draftSavedAt?: string | null;
  aiGenerationError?: string | null;
  aiProviderUsed?: string | null;
  aiInputTokens?: number | null;
  aiOutputTokens?: number | null;
  ownerUserId: string;
}

export interface InboxUnreadCount {
  totalUnread: number;
  orphanCount: number;
}

// === Day 8: conversation (thread) types ===
export interface InboxThreadListItem {
  threadId: string;
  participantEmail: string;
  participantName?: string | null;
  subject: string;
  latestPreview?: string | null;
  lastActivityAt: string;
  messageCount: number;
  unreadCount: number;
  aiCategory?: string | null;
  hasAiSuggestion: boolean;
  matchedCampaignId?: string | null;
  matchedCampaignName?: string | null;
  smtpGroupName?: string | null;
  isOrphan: boolean;
  latestInboundMessageId?: string | null;
}

export interface ThreadMessage {
  id: string;
  direction: 'in' | 'out';
  fromEmail: string;
  fromName?: string | null;
  toEmail: string;
  subject: string;
  htmlBody?: string | null;
  textBody?: string | null;
  at: string;
  isRead: boolean;
  aiCategory?: string | null;
}

export interface InboxThreadDetail {
  threadId: string;
  subject: string;
  participantEmail: string;
  participantName?: string | null;
  smtpGroupName?: string | null;
  matchedCampaignId?: string | null;
  matchedCampaignName?: string | null;
  messages: ThreadMessage[];
  latestInboundMessageId?: string | null;
  aiCategory?: string | null;
  aiSummary?: string | null;
  aiSuggestedReply?: string | null;
  userEditedReply?: string | null;
  aiGenerationError?: string | null;
  aiProviderUsed?: string | null;
  aiInputTokens?: number | null;
  aiOutputTokens?: number | null;
  aiGeneratedAt?: string | null;
  aiSuggestedQuestions?: string[];
}

// Day 9: Ask-AI chat
export interface AiChatTurn {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
  providerUsed?: string | null;
  inputTokens?: number | null;
  outputTokens?: number | null;
  suggestions?: string[];
}
