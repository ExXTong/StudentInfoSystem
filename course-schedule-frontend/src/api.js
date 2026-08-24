const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';

const request = async (path, options = {}) => {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers || {}),
    },
  });

  let data = null;
  try {
    data = await response.json();
  } catch {
    data = null;
  }

  if (!response.ok) {
    throw {
      success: false,
      status: response.status,
      error: data || { message: response.statusText },
    };
  }

  return data;
};

export const login = async (username, password) => {
  return request('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  });
};

export const getSchedule = async (token, username, password, year, term, tableType = 'std') => {
  return request('/schedule/get', {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: JSON.stringify({ username, password, year, term, tableType }),
  });
};

export const getGrades = async (token, username, password, year, term, allTerms = false) => {
  return request('/grade/query', {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: JSON.stringify({ username, password, year, term, allTerms }),
  });
};

export const getStudentInfo = async (token, username, password) => {
  return request('/student/crawler', {
    method: 'POST',
    headers: { Authorization: `Bearer ${token}` },
    body: JSON.stringify({ username, password }),
  });
};

export const getHealth = async (token) => {
  return request('/schedule/health', {
    method: 'GET',
    headers: { Authorization: `Bearer ${token}` },
  });
};

// Admin APIs
export const getAdminUsers = (token) => request('/admin/users', { headers: { Authorization: `Bearer ${token}` } });
export const clearAdminCache = (token) => request('/admin/cache/clear', { method: 'POST', headers: { Authorization: `Bearer ${token}` } });
export const getAdminStats = (token) => request('/admin/stats', { headers: { Authorization: `Bearer ${token}` } });
export const getAdminAnnouncements = (token) => request('/admin/announcements', { headers: { Authorization: `Bearer ${token}` } });
export const createAdminAnnouncement = (token, title, content) => request('/admin/announcements', {
  method: 'POST',
  headers: { Authorization: `Bearer ${token}` },
  body: JSON.stringify({ title, content }),
});
export const deleteAdminAnnouncement = (token, id) => request(`/admin/announcements/${id}`, {
  method: 'DELETE',
  headers: { Authorization: `Bearer ${token}` },
});
export const getAdminSettings = (token) => request('/admin/settings', { headers: { Authorization: `Bearer ${token}` } });
export const updateAdminSettings = (token, settings) => request('/admin/settings', {
  method: 'POST',
  headers: { Authorization: `Bearer ${token}` },
  body: JSON.stringify(settings),
});

export const getAnnouncements = () => request('/announcements');

export const getAdminDisabledUsers = (token) => request('/admin/users/disabled', { headers: { Authorization: `Bearer ${token}` } });
export const disableAdminUser = (token, username) => request(`/admin/users/${encodeURIComponent(username)}/disable`, { method: 'POST', headers: { Authorization: `Bearer ${token}` } });
export const enableAdminUser = (token, username) => request(`/admin/users/${encodeURIComponent(username)}/enable`, { method: 'POST', headers: { Authorization: `Bearer ${token}` } });
export const resetAdminUser = (token, username) => request(`/admin/users/${encodeURIComponent(username)}/reset`, { method: 'POST', headers: { Authorization: `Bearer ${token}` } });

export const getLoginHistory = (token) => request('/admin/login-history', { headers: { Authorization: `Bearer ${token}` } });
