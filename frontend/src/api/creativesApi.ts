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
  // channels: subset of whatsapp/instagram/facebook/email; omit/empty = all.
  content: (brief: string, channels?: string[]) =>
    axiosInstance.post('/creatives/content', { brief, channels: channels && channels.length ? channels : null }),
};

export interface MarketingContent {
  whatsApp: { broadcast: string; statusText: string };
  instagram: { caption: string; reelsHook: string; storyCta: string };
  facebook: { post: string; headline: string; cta: string };
  email: { subject: string; preview: string; body: string };
  imagePrompt: string;
  provider: string;
}
