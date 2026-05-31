import axios from 'axios';
import toast from 'react-hot-toast';

const axiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5211/api/v1',
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' },
});

axiosInstance.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('accessToken');
    if (token) config.headers.Authorization = `Bearer ${token}`;
    config.headers['X-Correlation-Id'] = crypto.randomUUID();
    return config;
  },
  (error) => Promise.reject(error)
);

axiosInstance.interceptors.response.use(
  (response) => response.data,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;
      try {
        const refreshToken = localStorage.getItem('refreshToken');
        const response = await axios.post(
          `${axiosInstance.defaults.baseURL}/auth/refresh`,
          { refreshToken }
        );
        const data = response.data.data;
        localStorage.setItem('accessToken', data.accessToken);
        localStorage.setItem('refreshToken', data.refreshToken);
        return axiosInstance(originalRequest);
      } catch {
        localStorage.clear();
        window.location.href = '/login';
      }
    }

    // Only show toast if the caller doesn't handle errors themselves
    if (!originalRequest?._silent) {
      const status = error.response?.status;
      const method = (originalRequest?.method || 'get').toLowerCase();
      // A 404 on a GET means "this resource isn't here" — the calling page renders its own
      // not-found / empty state for that. Auto-toasting the raw backend message ("X with key
      // (...) was not found.") just spams the user, especially when React Query refetches a
      // stale/deleted resource on an interval (StrictMode + retry multiply it). Suppress it.
      const isGetNotFound = status === 404 && method === 'get';
      if (!isGetNotFound) {
        const message = error.response?.data?.message || 'Something went wrong';
        toast.error(message);
      }
    }
    return Promise.reject(error);
  }
);

export default axiosInstance;
