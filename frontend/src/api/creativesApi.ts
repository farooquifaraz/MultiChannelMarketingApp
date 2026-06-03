import axiosInstance from './axiosInstance';

export interface GeneratedAsset {
  id: string;
  prompt: string;
  provider: string;
  size: string;
  width: number;
  height: number;
  status: 'pending' | 'completed' | 'failed';
  imageUrl?: string | null;
  creditCost: number;
  errorMessage?: string | null;
  createdAt: string;
}

export interface ImageSizeOption {
  token: string;
  width: number;
  height: number;
  label: string;
}

export const creativesApi = {
  sizes: () => axiosInstance.get('/creatives/sizes'),
  generate: (prompt: string, size: string) =>
    axiosInstance.post('/creatives/generate', { prompt, size }),
  assets: (take = 50) => axiosInstance.get('/creatives/assets', { params: { take } }),
};
