import React, { useState, useEffect } from 'react';
import { getGrades } from '../api';
import './GradeView.css';

const YEARS = ['2022-2023', '2023-2024', '2024-2025', '2025-2026', '2026-2027'];
const TERMS = [
  { value: '1', label: '第一学期' },
  { value: '2', label: '第二学期' },
];

const GradeView = ({ authToken, currentUser, cachedData, onCacheUpdate }) => {
  const [year, setYear] = useState('2025-2026');
  const [term, setTerm] = useState('2');
  const [allTerms, setAllTerms] = useState(false);
  const [grades, setGrades] = useState(cachedData || null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);
  const [passwordInput, setPasswordInput] = useState('');

  useEffect(() => {
    setGrades(cachedData || null);
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
      setError('请先输入密码后再更新成绩');
      setLoading(false);
      return;
    }

    try {
      const response = await getGrades(
        authToken,
        currentUser.username,
        passwordInput,
        year,
        term,
        allTerms,
      );
      if (response?.success) {
        const data = response.gradeSummary;
        data._meta = { year, term, allTerms };
        setGrades(data);
        onCacheUpdate?.(data);
      } else {
        setError(response?.error?.message || response?.message || '获取成绩失败');
      }
    } catch (apiError) {
      setError(apiError.error?.message || apiError.message || '获取成绩失败');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="grade-view">
      <div className="grade-controls">
        <div className="control-group">
          <label>学年</label>
          <select value={year} onChange={(e) => setYear(e.target.value)} disabled={allTerms}>
            {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
        </div>
        <div className="control-group">
          <label>学期</label>
          <select value={term} onChange={(e) => setTerm(e.target.value)} disabled={allTerms}>
            {TERMS.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={allTerms}
            onChange={(e) => setAllTerms(e.target.checked)}
          />
          所有学期
        </label>
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
          {loading ? '更新中...' : (cachedData ? '强制更新' : '查询成绩')}
        </button>
      </div>

      {error && <div className="grade-error">{error}</div>}

      {grades && (
        <div className="grade-summary">
          <p>总学分：{grades.totalCredits ?? '-'}</p>
          <p>平均绩点：{grades.averageGradePoint ?? '-'}</p>
          <p>平均分：{grades.averageScore ?? '-'}</p>
          <p>课程数：{grades.totalCourses ?? '-'}</p>
        </div>
      )}

      {grades?.scoreDistribution && (
        <div className="grade-distribution">
          <h3>成绩分布</h3>
          <div className="distribution-bars">
            {Object.entries(grades.scoreDistribution).map(([label, count]) => {
              const max = Math.max(1, ...Object.values(grades.scoreDistribution));
              return (
                <div key={label} className="distribution-row">
                  <span className="distribution-label">{label}</span>
                  <div className="distribution-bar-track">
                    <div
                      className="distribution-bar"
                      style={{ width: `${(count / max) * 100}%` }}
                    />
                  </div>
                  <span className="distribution-count">{count}</span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {grades?.grades?.length > 0 ? (
        <table className="grade-table">
          <thead>
            <tr>
              <th>学年学期</th>
              <th>课程名称</th>
              <th>课程类别</th>
              <th>学分</th>
              <th>成绩</th>
              <th>绩点</th>
            </tr>
          </thead>
          <tbody>
            {grades.grades.map((g, i) => (
              <tr key={`${g.courseCode}-${g.courseName}-${i}`}>
                <td>{g.year} {g.term}</td>
                <td>{g.courseName}</td>
                <td>{g.courseType}</td>
                <td>{g.credits}</td>
                <td>{g.gradeValue}</td>
                <td>{g.gradePoint}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        !error && grades && <p className="grade-empty">暂无成绩数据</p>
      )}
    </div>
  );
};

export default GradeView;
