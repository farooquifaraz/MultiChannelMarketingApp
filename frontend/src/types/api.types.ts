export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errors?: string[];
  correlationId?: string;
  timestamp: string;
}

export interface PagedResponse<T> {
  success: boolean;
  message: string;
  data: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}
