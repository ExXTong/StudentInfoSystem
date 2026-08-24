import React, { useState, useEffect, useCallback } from 'react';
import {
  getHealth,
  getAdminUsers,
  getAdminDisabledUsers,
  getLoginHistory,
  disableAdminUser,
  enableAdminUser,
  resetAdminUser,
  clearAdminCache,
  getAdminStats,
  getAdminAnnouncements,
  createAdminAnnouncement,
  deleteAdminAnnouncement,
  getAdminSettings,
  updateAdminSettings,
} from '../api';
import './AdminView.css';

const AdminView = ({ currentUser, authToken }) => {
  const [health, setHealth] = useState(null);
  const [healthError, setHealthError] = useState(null);
  const [cacheInfo, setCacheInfo] = useState([]);
  const [users, setUsers] = useState([]);
  const [disabledUsers, setDisabledUsers] = useState([]);
  const [loginHistory, setLoginHistory] = useState([]);
  const [stats, setStats] = useState(null);
  const [announcements, setAnnouncements] = useState([]);
  const [annTitle, setAnnTitle] = useState('');
  const [annContent, setAnnContent] = useState('');
  const [settingsText, setSettingsText] = useState('');
  const [message, setMessage] = useState('');

  const username = currentUser?.username || 'anonymous';

  const refreshCacheInfo = useCallback(() => {
    const types = ['schedule', 'grades', 'profile'];
    const list = types.map((type) => {
      const key = `sis:${username}:${type}`;
      const raw = localStorage.getItem(key);
      let size = 0;
      if (raw) size = new Blob([raw]).size;
      return { type, key, size, exists: !!raw };
    });
    setCacheInfo(list);
  }, [username]);

  const loadAdmin = useCallback(async () => {
    if (!authToken) return;
    try {
      const [u, d, s, a, st, h] = await Promise.all([
        getAdminUsers(authToken),
        getAdminDisabledUsers(authToken),
        getAdminStats(authToken),
        getAdminAnnouncements(authToken),
        getAdminSettings(authToken),
        getLoginHistory(authToken),
      ]);
      setUsers(u?.users || []);
      setDisabledUsers(d || []);
      setStats(s || null);
      setLoginHistory(h || []);
      setAnnouncements(a || []);
      setSettingsText(JSON.stringify(st || {}, null, 2));
    } catch (e) {
      setMessage(e.error?.message || e.message || '加载管理数据失败');
    }
  }, [authToken]);

  useEffect(() => {
    refreshCacheInfo();
    const loadHealth = async () => {
      try {
        setHealth(await getHealth(authToken));
        setHealthError(null);
      } catch (e) {
        setHealthError(e.error?.message || e.message || '获取服务状态失败');
      }
    };
    if (authToken) {
      loadHealth();
      loadAdmin();
    }
  }, [authToken, username, refreshCacheInfo, loadAdmin]);

  const clearCache = (type) => {
    if (type) {
      localStorage.removeItem(`sis:${username}:${type}`);
    } else {
      ['schedule', 'grades', 'profile'].forEach((t) => localStorage.removeItem(`sis:${username}:${t}`));
    }
    refreshCacheInfo();
  };

  const handleDisableUser = async (username) => {
    try {
      await disableAdminUser(authToken, username);
      setMessage(`用户 ${username} 已禁用`);
      await loadAdmin();
    } catch (e) {
      setMessage(e.error?.message || e.message || '操作失败');
    }
  };

  const handleEnableUser = async (username) => {
    try {
      await enableAdminUser(authToken, username);
      setMessage(`用户 ${username} 已启用`);
      await loadAdmin();
    } catch (e) {
      setMessage(e.error?.message || e.message || '操作失败');
    }
  };

  const handleResetUser = async (username) => {
    try {
      await resetAdminUser(authToken, username);
      setMessage(`用户 ${username} 缓存已重置`);
      await loadAdmin();
    } catch (e) {
      setMessage(e.error?.message || e.message || '操作失败');
    }
  };

  const handleClearServerCache = async () => {
    try {
      const res = await clearAdminCache(authToken);
      setMessage(res?.message || '服务端缓存已清除');
      await loadAdmin();
    } catch (e) {
      setMessage(e.error?.message || e.message || '清除失败');
    }
  };

  const handleAddAnnouncement = async () => {
    if (!annTitle.trim() || !annContent.trim()) {
      setMessage('标题和内容不能为空');
      return;
    }
    try {
      await createAdminAnnouncement(authToken, annTitle, annContent);
      setAnnTitle('');
      setAnnContent('');
      setMessage('公告已发布');
      await loadAdmin();
    } catch (e) {
      setMessage(e.error?.message || e.message || '发布失败');
    }
  };

  const handleDeleteAnnouncement = async (id) => {
    try {
      await deleteAdminAnnouncement(authToken, id);
      setMessage('公告已删除');
      await loadAdmin();
    } catch (e) {
      setMessage(e.error?.message || e.message || '删除失败');
    }
  };

  const handleSaveSettings = async () => {
    try {
      const parsed = JSON.parse(settingsText || '{}');
      await updateAdminSettings(authToken, parsed);
      setMessage('设置已保存');
      await loadAdmin();
    } catch (e) {
      setMessage(e instanceof SyntaxError ? '设置必须是合法 JSON' : (e.error?.message || e.message || '保存失败'));
    }
  };

  const handleExport = () => {
    const data = {};
    ['schedule', 'grades', 'profile'].forEach((type) => {
      const key = `sis:${username}:${type}`;
      const raw = localStorage.getItem(key);
      if (raw) data[key] = raw;
    });
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `student-data-${username}.json`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleImport = (event) => {
    const file = event.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const data = JSON.parse(reader.result);
        Object.entries(data).forEach(([key, value]) => {
          if (key.startsWith('sis:')) localStorage.setItem(key, value);
        });
        setMessage('本地数据导入成功');
        refreshCacheInfo();
      } catch {
        setMessage('导入失败：文件格式不正确');
      }
    };
    reader.readAsText(file);
    event.target.value = '';
  };

  return (
    <div className="admin-view">
      <h2>管理页面</h2>

      {message && <div className="admin-message">{message}</div>}

      <section className="admin-section">
        <h3>当前用户</h3>
        <p>学号：{currentUser?.username || '-'}</p>
        <p>姓名：{currentUser?.name || '-'}</p>
        <p>角色：{currentUser?.role || '-'}</p>
      </section>

      <section className="admin-section">
        <h3>系统状态</h3>
        {health ? (
          <div className="health-card ok">
            <p>状态：{health.status}</p>
            <p>服务：{health.service}</p>
          </div>
        ) : healthError ? (
          <div className="health-card error">{healthError}</div>
        ) : (
          <p>正在检测服务状态...</p>
        )}
      </section>

      <section className="admin-section">
        <h3>用户管理</h3>
        {users.length === 0 ? (
          <p>暂无活跃用户</p>
        ) : (
          <ul className="admin-user-list">
            {users.map((u) => (
              <li key={u} className="user-row">
                <span>{u}</span>
                <div className="user-actions">
                  <button onClick={() => handleDisableUser(u)}>禁用</button>
                  <button onClick={() => handleResetUser(u)}>重置</button>
                </div>
              </li>
            ))}
          </ul>
        )}

        {disabledUsers.length > 0 && (
          <div className="disabled-users">
            <h4>已禁用用户</h4>
            <ul className="admin-user-list">
              {disabledUsers.map((u) => (
                <li key={u} className="user-row">
                  <span>{u}</span>
                  <button onClick={() => handleEnableUser(u)}>启用</button>
                </li>
              ))}
            </ul>
          </div>
        )}
        <button className="secondary-button" onClick={handleClearServerCache}>清除服务端缓存</button>
      </section>

      <section className="admin-section">
        <h3>访问统计</h3>
        {stats ? (
          <div className="stats-grid">
            <p>登录次数：{stats.totalLogins ?? 0}</p>
            <p>查询次数：{stats.totalQueries ?? 0}</p>
            <p>最近登录：{stats.lastLogin || '-'}</p>
            <p>最近时间：{stats.lastLoginTime ? new Date(stats.lastLoginTime).toLocaleString() : '-'}</p>
          </div>
        ) : (
          <p>暂无统计数据</p>
        )}
      </section>

      <section className="admin-section">
        <h3>登录历史</h3>
        {loginHistory.length === 0 ? (
          <p>暂无登录记录</p>
        ) : (
          <table className="login-history-table">
            <thead>
              <tr><th>用户</th><th>时间</th></tr>
            </thead>
            <tbody>
              {loginHistory.slice(-20).reverse().map((item, i) => (
                <tr key={i}>
                  <td>{item.username}</td>
                  <td>{new Date(item.time).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="admin-section">
        <h3>公告管理</h3>
        <div className="announcement-form">
          <input
            placeholder="公告标题"
            value={annTitle}
            onChange={(e) => setAnnTitle(e.target.value)}
          />
          <textarea
            placeholder="公告内容"
            value={annContent}
            onChange={(e) => setAnnContent(e.target.value)}
            rows={3}
          />
          <button onClick={handleAddAnnouncement}>发布公告</button>
        </div>
        {announcements.length > 0 && (
          <ul className="announcement-list">
            {announcements.map((a) => (
              <li key={a.id}>
                <strong>{a.title}</strong>
                <span>{a.content}</span>
                <button onClick={() => handleDeleteAnnouncement(a.id)}>删除</button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="admin-section">
        <h3>系统参数配置</h3>
        <textarea
          className="settings-editor"
          rows={8}
          value={settingsText}
          onChange={(e) => setSettingsText(e.target.value)}
        />
        <button onClick={handleSaveSettings}>保存设置</button>
      </section>

      <section className="admin-section">
        <h3>数据备份</h3>
        <div className="backup-actions">
          <button onClick={handleExport}>导出本地数据</button>
          <label className="import-button">
            导入本地数据
            <input type="file" accept="application/json" onChange={handleImport} />
          </label>
        </div>
      </section>

      <section className="admin-section">
        <h3>本地缓存管理</h3>
        <div className="cache-list">
          {cacheInfo.map((item) => (
            <div key={item.type} className="cache-item">
              <div>
                <strong>{item.type}</strong>
                <span>{item.exists ? `${item.size} bytes` : '无缓存'}</span>
              </div>
              <button onClick={() => clearCache(item.type)}>清除</button>
            </div>
          ))}
        </div>
        <button className="clear-all" onClick={() => clearCache(null)}>清除全部本地缓存</button>
      </section>
    </div>
  );
};

export default AdminView;
