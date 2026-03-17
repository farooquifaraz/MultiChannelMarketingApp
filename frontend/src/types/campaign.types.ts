export interface CampaignDto {
  id: string;
  name: string;
  channel: string;
  status: string;
  templateName?: string;
  groupName?: string;
  totalContacts: number;
  sentCount: number;
  failedCount: number;
  scheduledAt?: string;
  completedAt?: string;
  createdAt: string;
}

export interface CampaignDetailDto extends CampaignDto {
  templateId: string;
  groupId: string;
  startedAt?: string;
}

export interface CreateCampaignDto {
  name: string;
  templateId: string;
  channel: string;
  groupId: string;
  scheduledAt?: string;
}

export interface CampaignReportDto {
  campaignId: string;
  campaignName: string;
  channel: string;
  status: string;
  totalContacts: number;
  sentCount: number;
  failedCount: number;
  deliveredCount: number;
  openedCount: number;
  sendRate: number;
  failRate: number;
  startedAt?: string;
  completedAt?: string;
}

export interface CampaignMessageDto {
  id: string;
  contactName: string;
  contactEmail?: string;
  contactPhone?: string;
  status: string;
  errorMessage?: string;
  sentAt?: string;
  deliveredAt?: string;
}

export interface DashboardStatsDto {
  totalContacts: number;
  totalCampaigns: number;
  totalMessagesSent: number;
  activeCampaigns: number;
  overallSuccessRate: number;
}

export interface ChannelBreakdownDto {
  email: ChannelStatsDto;
  whatsApp: ChannelStatsDto;
  sms: ChannelStatsDto;
}

export interface ChannelStatsDto {
  campaignCount: number;
  messagesSent: number;
  messagesFailed: number;
  successRate: number;
}
