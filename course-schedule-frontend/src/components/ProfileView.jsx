import React, { useState, useEffect } from 'react';
import { getStudentInfo } from '../api';
import './ProfileView.css';

const ProfileView = ({ authToken, currentUser, cachedData, onCacheUpdate }) => {
  const [info, setInfo] = useState(cachedData || null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);
  const [passwordInput, setPasswordInput] = useState('');

  useEffect(() => {
    setInfo(cachedData || null);
  }, [cachedData]);

  const handleFetch = async () => {
    setError(null);
    setLoading(true);

    if (!currentUser || !authToken) {
      setError('请先登录');
      setLoading(false);
      return;
    }

    if (!passwordInput) {
      setError('请先输入密码后再更新个人信息');
      setLoading(false);
      return;
    }

    try {
      const response = await getStudentInfo(authToken, currentUser.username, passwordInput);
      const data = response?.data || response;
      if (data) {
        setInfo(data);
        onCacheUpdate?.(data);
      } else {
        setError(response?.message || '获取个人信息失败');
      }
    } catch (apiError) {
      setError(apiError.error?.message || apiError.message || '获取个人信息失败');
    } finally {
      setLoading(false);
    }
  };

  const fields = info
    ? [
        ['学号', info.studentId],
        ['姓名', info.name],
        ['性别', info.gender],
        ['院系', info.department],
        ['专业', info.major],
        ['年级', info.grade],
        ['班级', info.class],
        ['邮箱', info.email],
        ['手机', info.mobile || info.phone],
      ]
    : [];

  return (
    <div className="profile-view">
      <div className="control-group">
        <label>密码</label>
        <input
          type="password"
          value={passwordInput}
          onChange={(e) => setPasswordInput(e.target.value)}
          placeholder="更新数据需输入密码"
        />
      </div>
      <button onClick={handleFetch} disabled={loading}>
        {loading ? '更新中...' : (cachedData ? '强制更新' : '加载个人信息')}
      </button>

      {error && <div className="profile-error">{error}</div>}

      {info ? (
        <div className="profile-card">
          <h3>{info.name || '学生'}</h3>
          <table className="profile-table">
            <tbody>
              {fields.map(([label, value]) => (
                <tr key={label}>
                  <td>{label}</td>
                  <td>{value || '-'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        !error && <p className="profile-empty">暂无缓存，点击按钮加载个人信息</p>
      )}
    </div>
  );
};

export default ProfileView;
