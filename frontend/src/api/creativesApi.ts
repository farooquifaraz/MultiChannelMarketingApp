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

export interface PromptPreset {
  title: string;
  prompt: string;
}

export const creativesApi = {
  sizes: () => axiosInstance.get('/creatives/sizes'),
  presets: () => axiosInstance.get('/creatives/presets'),
  generate: (prompt: string, size: string, brandKitId?: string | null) =>
    axiosInstance.post('/creatives/generate', { prompt, size, brandKitId: brandKitId || null }),
  assets: (take = 50) => axiosInstance.get('/creatives/assets', { params: { take } }),
  deleteAsset: (id: string) => axiosInstance.delete(`/creatives/assets/${id}`),
  content: (brief: string) => axiosInstance.post('/creatives/content', { brief }),
};

export interface MarketingContent {
  whatsApp: { broadcast: string; statusText: string };
  instagram: { caption: string; reelsHook: string; storyCta: string };
  email: { subject: string; preview: string; body: string };
  imagePrompt: string;
  provider: string;
}
