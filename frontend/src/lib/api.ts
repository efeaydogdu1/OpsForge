import axios from "axios";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:8080/api/v1";

export const api = axios.create({ baseURL: API_BASE, withCredentials: true });

api.interceptors.request.use((config) => {
  const token = typeof window !== "undefined" ? localStorage.getItem("opsforge.token") : null;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (r) => r,
  async (err) => {
    if (err.response?.status === 401) {
      try {
        const res = await axios.post(`${API_BASE}/auth/refresh`, {}, { withCredentials: true });
        const newToken: string = res.data.accessToken;
        localStorage.setItem("opsforge.token", newToken);
        err.config.headers.Authorization = `Bearer ${newToken}`;
        return axios(err.config);
      } catch {
        localStorage.removeItem("opsforge.token");
        window.location.href = "/login";
      }
    }
    return Promise.reject(err);
  }
);

export const authApi = {
  register: (data: { email: string; password: string; displayName: string }) =>
    api.post("/auth/register", data).then((r) => r.data),
  login: (data: { email: string; password: string }) =>
    api.post("/auth/login", data).then((r) => r.data),
  logout: () => { localStorage.removeItem("opsforge.token"); },
};

export const teamsApi = {
  list: () => api.get("/teams").then((r) => r.data),
  get: (id: string) => api.get(`/teams/${id}`).then((r) => r.data),
  create: (data: { name: string; description?: string }) => api.post("/teams", data).then((r) => r.data),
  update: (id: string, data: { name: string; description?: string }) => api.put(`/teams/${id}`, data).then((r) => r.data),
  delete: (id: string) => api.delete(`/teams/${id}`),
};

export const servicesApi = {
  list: () => api.get("/services").then((r) => r.data),
  get: (id: string) => api.get(`/services/${id}`).then((r) => r.data),
  create: (data: object) => api.post("/services", data).then((r) => r.data),
  update: (id: string, data: object) => api.put(`/services/${id}`, data).then((r) => r.data),
  delete: (id: string) => api.delete(`/services/${id}`),
  healthCheck: (id: string) => api.post(`/services/${id}/health-check`, {}).then((r) => r.data),
};

export const githubApi = {
  preview: (repositoryUrl: string) =>
    api.post("/github/preview", { repositoryUrl }).then((r) => r.data),
  link: (serviceId: string, repositoryUrl: string) =>
    api.post(`/github/services/${serviceId}/link`, { repositoryUrl }).then((r) => r.data),
  sync: (serviceId: string) =>
    api.post(`/github/services/${serviceId}/sync`, {}).then((r) => r.data),
  listSyncRuns: (serviceId: string) =>
    api.get(`/github/services/${serviceId}/sync-runs`).then((r) => r.data),
};

export const environmentsApi = {
  list: (serviceId?: string) => api.get("/environments", { params: { serviceId } }).then((r) => r.data),
  create: (data: object) => api.post("/environments", data).then((r) => r.data),
  update: (id: string, data: object) => api.put(`/environments/${id}`, data).then((r) => r.data),
  delete: (id: string) => api.delete(`/environments/${id}`),
};

export const infraApi = {
  list: () => api.get("/infrastructure").then((r) => r.data),
  create: (data: object) => api.post("/infrastructure", data).then((r) => r.data),
  update: (id: string, data: object) => api.put(`/infrastructure/${id}`, data).then((r) => r.data),
  delete: (id: string) => api.delete(`/infrastructure/${id}`),
  link: (assetId: string, serviceId: string) => api.post(`/infrastructure/${assetId}/link/${serviceId}`),
  unlink: (assetId: string, serviceId: string) => api.delete(`/infrastructure/${assetId}/link/${serviceId}`),
};

export const deploymentsApi = {
  list: (serviceId?: string) => api.get("/deployments", { params: { serviceId } }).then((r) => r.data),
  create: (data: object) => api.post("/deployments", data).then((r) => r.data),
};

export const auditApi = {
  list: (params?: { entityType?: string; userId?: string }) =>
    api.get("/audit", { params }).then((r) => r.data),
};
