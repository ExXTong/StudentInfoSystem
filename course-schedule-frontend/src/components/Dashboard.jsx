import React, { useState, useEffect, useCallback } from 'react';
import ScheduleView from './ScheduleView';
import GradeView from './GradeView';
import ProfileView from './ProfileView';
import { getAnnouncements } from '../api';
import AdminView from './AdminView';
import './Dashboard.css';

const ADMIN_USERNAMES = ['root'];

const readCache = (key) => {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
};

const writeCache = (key, data) => {
  try {
    localStorage.setItem(key, JSON.stringify(data));
  } catch {
    // ignore quota errors
  }
};

const Dashboard = ({ authToken, currentUser, onLogout }) => {
  const [activeTab, setActiveTab] = useState(() => currentUser?.username === 'root' ? 'admin' : 'schedule');
  const [dark, setDark] = useState(() => {
    const saved = localStorage.getItem('sis:theme');
    return saved ? saved === 'dark' : false;
  });

  const username = currentUser?.username || 'anonymous';
  const cacheKey = useCallback((type) => `sis:${username}:${type}`, [username]);

  const [scheduleCache, setScheduleCache] = useState(() => readCache(cacheKey('schedule')));
  const [gradeCache, setGradeCache] = useState(() => readCache(cacheKey('grades')));
  const [profileCache, setProfileCache] = useState(() => readCache(cacheKey('profile')));
  const [announcements, setAnnouncements] = useState([]);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    localStorage.setItem('sis:theme', dark ? 'dark' : 'light');
  }, [dark]);

  useEffect(() => {
    getAnnouncements()
      .then((data) => setAnnouncements(Array.isArray(data) ? data : []))
      .catch(() => setAnnouncements([]));
  }, []);

  useEffect(() => {
    setScheduleCache(readCache(cacheKey('schedule')));
    setGradeCache(readCache(cacheKey('grades')));
    setProfileCache(readCache(cacheKey('profile')));
  }, [username, cacheKey]);

  const saveSchedule = (data) => {
    setScheduleCache(data);
    writeCache(cacheKey('schedule'), data);
  };

  const saveGrades = (data) => {
    setGradeCache(data);
    writeCache(cacheKey('grades'), data);
  };

  const saveProfile = (data) => {
    setProfileCache(data);
    writeCache(cacheKey('profile'), data);
  };

  const isAdmin = ADMIN_USERNAMES.includes(currentUser?.username);
  const tabs = isAdmin
    ? [{ key: 'admin', label: '管理' }]
    : [
        { key: 'schedule', label: '课表' },
        { key: 'grades', label: '成绩' },
        { key: 'profile', label: '个人信息' },
      ];

  return (
    <div className="dashboard-container">
      <header className="dashboard-header">
        <div className="dashboard-user">
          <h2>{currentUser?.name || currentUser?.username || '学生'}</h2>
          <span>{currentUser?.role || 'Student'}</span>
        </div>
        <div className="header-actions">
          <button className="theme-toggle" onClick={() => setDark(!dark)}>
            {dark ? '☀️ 亮色' : '🌙 暗色'}
          </button>
          <button className="logout-button" onClick={onLogout}>退出登录</button>
        </div>
      </header>

      {announcements.length > 0 && (
        <div className="announcement-banner">
          {announcements.map((a) => (
            <div key={a.id} className="announcement-item">
              <strong>{a.title}</strong>
              <span>{a.content}</span>
            </div>
          ))}
        </div>
      )}

      <nav className="dashboard-tabs">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            className={activeTab === tab.key ? 'tab-button active' : 'tab-button'}
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </nav>

      <main className="dashboard-content">
        {activeTab === 'schedule' && (
          <ScheduleView
            authToken={authToken}
            currentUser={currentUser}
            cachedData={scheduleCache}
            onCacheUpdate={saveSchedule}
          />
        )}
        {activeTab === 'grades' && (
          <GradeView
            authToken={authToken}
            currentUser={currentUser}
            cachedData={gradeCache}
            onCacheUpdate={saveGrades}
          />
        )}
        {activeTab === 'admin' && isAdmin && (
          <AdminView
            authToken={authToken}
            currentUser={currentUser}
          />
        )}
        {activeTab === 'profile' && (
          <ProfileView
            authToken={authToken}
            currentUser={currentUser}
            cachedData={profileCache}
            onCacheUpdate={saveProfile}
          />
        )}
      </main>
    </div>
  );
};

export default Dashboard;
