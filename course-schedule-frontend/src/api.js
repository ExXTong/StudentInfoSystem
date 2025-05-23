const API_BASE_URL = 'https://localhost:10010/api';

export const login = async (username, password) => {
  const response = await fetch(`${API_BASE_URL}/auth/login`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ username, password }),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({ message: response.statusText }));
    throw { success: false, status: response.status, error: errorData };
  }

  return response.json();
};

export const getSchedule = async (token, username, password, year, term) => {
  const response = await fetch(`${API_BASE_URL}/schedule/get`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`,
    },
    body: JSON.stringify({ username, password, year, term }),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({ message: response.statusText }));
    throw { success: false, status: response.status, error: errorData };
  }

  return response.json();
};
