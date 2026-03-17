export interface ContactDto {
  id: string;
  fullName: string;
  email?: string;
  phone?: string;
  whatsAppNumber?: string;
  groupId?: string;
  groupName?: string;
  customFields?: Record<string, string>;
  isActive: boolean;
  createdAt: string;
}

export interface CreateContactDto {
  fullName: string;
  email?: string;
  phone?: string;
  whatsAppNumber?: string;
  groupId?: string;
  customFields?: Record<string, string>;
}

export interface ContactGroupDto {
  id: string;
  name: string;
  description?: string;
  contactCount: number;
  createdAt: string;
}

export interface TemplateDto {
  id: string;
  name: string;
  channel: string;
  subject?: string;
  body: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateTemplateDto {
  name: string;
  channel: string;
  subject?: string;
  body: string;
}

export interface AuthResponseDto {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserDto;
}

export interface UserDto {
  id: string;
  fullName: string;
  email: string;
  role: string;
}

export interface ImportResultDto {
  totalRows: number;
  successCount: number;
  failedCount: number;
  errors: string[];
}
