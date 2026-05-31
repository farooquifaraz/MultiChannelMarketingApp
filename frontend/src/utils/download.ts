import axiosInstance from '../api/axiosInstance';

/**
 * Fetch a CSV (or any) endpoint with the existing auth header and trigger a browser download.
 * Falls back to a generated filename if the server didn't send Content-Disposition.
 */
export async function downloadFile(url: string, fallbackFilename: string): Promise<void> {
  const response = await axiosInstance.get(url, { responseType: 'blob' });
  // axios returns the blob directly under response (because of response interceptor) or response.data
  const blob: Blob = (response as any).data ?? (response as any);

  // Try to read filename from Content-Disposition (e.g. attachment; filename="x.csv")
  const headers: any = (response as any).headers || {};
  const disposition = headers['content-disposition'] || headers['Content-Disposition'];
  let filename = fallbackFilename;
  if (disposition) {
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    if (match?.[1]) filename = decodeURIComponent(match[1]);
  }

  const objectUrl = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = objectUrl;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(objectUrl);
}
