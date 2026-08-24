import React, { useState, useEffect } from 'react';
import { getSchedule } from '../api';
import './ScheduleView.css';

const DAY_NAMES = ['周一', '周二', '周三', '周四', '周五', '周六', '周日'];
const YEARS = ['2022-2023', '2023-2024', '2024-2025', '2025-2026', '2026-2027'];
const WEEKS = Array.from({ length: 20 }, (_, i) => i + 1);
const TERMS = [
  { value: '1', label: '第一学期' },
  { value: '2', label: '第二学期' },
];

const parsePeriodRange = (course) => {
  const start = Number(course.startPeriod);
  const end = Number(course.endPeriod);
  if (Number.isInteger(start) && start > 0) {
    return {
      start,
      end: Number.isInteger(end) && end >= start ? end : start,
    };
  }

  const text = course.formattedPeriods || '';
  const match = text.match(/(\d+)-(\d+)/);
  if (match) {
    return { start: Number(match[1]), end: Number(match[2]) };
  }

  const single = text.match(/(\d+)/);
  const p = single ? Number(single[1]) : 1;
  return { start: p, end: p };
};

const ScheduleView = ({ authToken, currentUser, cachedData, onCacheUpdate }) => {
  const [year, setYear] = useState('2025-2026');
  const [term, setTerm] = useState('2');
  const [tableType, setTableType] = useState('std');
  const [week, setWeek] = useState(0);
  const [keyword, setKeyword] = useState('');
  const [passwordInput, setPasswordInput] = useState('');
  const [scheduleData, setScheduleData] = useState(cachedData || null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setScheduleData(cachedData || null);
  }, [cachedData]);

  const handleFetchSchedule = async () => {
    setError(null);
    setLoading(true);

    if (!currentUser || !authToken) {
      setError('请先登录');
      setLoading(false);
      return;
    }

    const { username } = currentUser;
    if (!passwordInput) {
      setError('请先输入密码后再更新课表');
      setLoading(false);
      return;
    }

    try {
      const response = await getSchedule(authToken, username, passwordInput, year, term, tableType);
      const courses = response?.courses || response?.schedule?.courses;

      if (response?.success && Array.isArray(courses)) {
        const data = { year, term, tableType, courses };
        setScheduleData(data);
        onCacheUpdate?.(data);
      } else {
        setError(response?.error?.message || response?.message || '获取课表失败');
      }
    } catch (apiError) {
      setError(apiError.error?.message || apiError.message || '获取课表失败');
    } finally {
      setLoading(false);
    }
  };

  const courses = scheduleData?.courses || null;
  const filteredByWeek = week === 0
    ? (courses || [])
    : (courses || []).filter((c) => !c.weeks || c.weeks.includes(week));

  const q = keyword.trim().toLowerCase();
  const visibleCourses = q
    ? filteredByWeek.filter((c) =>
        (c.courseName || c.name || '').toLowerCase().includes(q) ||
        (c.teacherName || c.instructor || '').toLowerCase().includes(q))
    : filteredByWeek;

  const todayJs = new Date().getDay();
  const today = todayJs === 0 ? 7 : todayJs;
  const todayCourses = visibleCourses.filter((c) => Number(c.dayOfWeek) === today);

  const buildTimetable = () => {
    if (!visibleCourses || visibleCourses.length === 0) return null;

    const maxPeriod = courses.reduce((max, c) => {
      const { end } = parsePeriodRange(c);
      return Math.max(max, end);
    }, 0) || 10;

    const grid = {};
    const spans = {};

    visibleCourses.forEach((course) => {
      const day = Number(course.dayOfWeek);
      if (!Number.isInteger(day) || day < 1 || day > 7) return;
      const { start, end } = parsePeriodRange(course);
      const span = Math.max(1, end - start + 1);
      const key = `${day}-${start}`;
      if (!grid[key]) {
        grid[key] = { course, span };
        spans[`${day}-${start}`] = span;
      }
    });

    return { maxPeriod, grid, spans };
  };

  const timetable = buildTimetable();

  return (
    <div className="schedule-view-container">
      <div className="schedule-controls">
        <div className="control-group">
          <label>学年</label>
          <select value={year} onChange={(e) => setYear(e.target.value)}>
            {YEARS.map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
        </div>
        <div className="control-group">
          <label>学期</label>
          <select value={term} onChange={(e) => setTerm(e.target.value)}>
            {TERMS.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
        <div className="control-group">
          <label>课表类型</label>
          <select value={tableType} onChange={(e) => setTableType(e.target.value)}>
            <option value="std">学生课表</option>
            <option value="class">班级课表</option>
          </select>
        </div>
        <div className="control-group">
          <label>教学周</label>
          <select value={week} onChange={(e) => setWeek(Number(e.target.value))}>
            <option value={0}>全部</option>
            {WEEKS.map((w) => <option key={w} value={w}>第{w}周</option>)}
          </select>
        </div>
        <div className="control-group">
          <label>搜索</label>
          <input
            type="text"
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            placeholder="课程 / 教师"
          />
        </div>
        <div className="control-group">
          <label>密码</label>
          <input
            type="password"
            value={passwordInput}
            onChange={(e) => setPasswordInput(e.target.value)}
            placeholder="更新数据需输入密码"
          />
        </div>
        <button onClick={handleFetchSchedule} disabled={loading}>
          {loading ? '更新中...' : (cachedData ? '强制更新' : '查询课表')}
        </button>
      </div>

      {error && <div className="schedule-error">{error}</div>}

      {todayCourses.length > 0 && (
        <div className="today-courses">
          <h3>📌 今日课程</h3>
          <div className="today-course-list">
            {todayCourses.map((c, i) => (
              <span key={`today-${c.courseNumber || c.courseId || i}`} className="today-course-tag">
                {c.courseName || c.name} · {c.classroom || '未安排教室'}
              </span>
            ))}
          </div>
        </div>
      )}

      {timetable ? (
        <div className="schedule-table-wrapper">
          <h2>{scheduleData.year || year} 学年第 {scheduleData.term || term} 学期</h2>
          <table className="timetable">
            <thead>
              <tr>
                <th className="time-column">节次</th>
                {DAY_NAMES.map((day) => <th key={day}>{day}</th>)}
              </tr>
            </thead>
            <tbody>
              {Array.from({ length: timetable.maxPeriod }, (_, i) => i + 1).map((period) => (
                <tr key={period}>
                  <td className="time-cell">第{period}节</td>
                  {DAY_NAMES.map((_, dayIndex) => {
                    const day = dayIndex + 1;
                    const cell = timetable.grid[`${day}-${period}`];
                    if (!cell) {
                      return <td key={`${day}-${period}`} className="empty-cell" />;
                    }

                    // 只在该课程起始节次渲染，跨节通过 rowSpan 占位
                    const isStart = period === parsePeriodRange(cell.course).start;
                    if (!isStart) {
                      return null;
                    }

                    return (
                      <td
                        key={`${day}-${period}`}
                        rowSpan={cell.span}
                        className="course-cell"
                      >
                        <div className="timetable-course">
                          <strong>{cell.course.courseName || cell.course.name}</strong>
                          <span>{cell.course.teacherName || cell.course.instructor || ''}</span>
                          <span>{cell.course.classroom || ''}</span>
                          {cell.course.weeks?.length > 0 && (
                            <span className="weeks">周次：{cell.course.weeks.join(',')}</span>
                          )}
                        </div>
                      </td>
                    );
                  })}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        !error && !loading && <p className="loading-message">暂无缓存，请点击“查询课表”获取数据</p>
      )}
    </div>
  );
};

export default ScheduleView;
