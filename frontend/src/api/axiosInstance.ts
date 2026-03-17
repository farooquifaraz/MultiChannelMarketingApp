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
      const message = error.response?.data?.message || 'Something went wrong';
      toast.error(message);
    }
    return Promise.reject(error);
  }
);

export default axiosInstance;
