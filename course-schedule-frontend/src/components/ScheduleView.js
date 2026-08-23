import React, { useState } from 'react';
import { getSchedule } from '../api';
import './ScheduleView.css';

const DAY_NAMES = ['周一', '周二', '周三', '周四', '周五', '周六', '周日'];

const ScheduleView = ({ authToken, currentUser }) => {
  const [year, setYear] = useState('2024-2025');
  const [term, setTerm] = useState('1');
  const [scheduleData, setScheduleData] = useState(null);
  const [error, setError] = useState(null);

  const handleFetchSchedule = async () => {
    setError(null);
    setScheduleData(null);

    if (!currentUser || !authToken) {
      setError('User not logged in or token missing.');
      return;
    }

    const { username, password } = currentUser;

    try {
      const response = await getSchedule(authToken, username, password, year, term);
      const courses = response?.courses || response?.schedule?.courses;

      if (response?.success && Array.isArray(courses)) {
        setScheduleData(courses);
      } else {
        setError(response?.error?.message || response?.message || 'Failed to fetch schedule. Invalid data format.');
      }
    } catch (apiError) {
      setError(apiError.error?.message || apiError.message || 'An unexpected error occurred while fetching schedule.');
    }
  };

  const formatDay = (day) => {
    const index = Number(day);
    if (Number.isInteger(index) && index >= 1 && index <= 7) {
      return DAY_NAMES[index - 1];
    }
    return day || '';
  };

  return (
    <div className="schedule-view-container">
      {currentUser && (
        <div className="user-welcome">
          <p>Welcome, {currentUser.name || currentUser.username}!</p>
          {currentUser.role && <p>Role: {currentUser.role}</p>}
        </div>
      )}
      <div className="schedule-controls">
        <div>
          <label htmlFor="year">Year:</label>
          <input
            type="text"
            id="year"
            value={year}
            onChange={(e) => setYear(e.target.value)}
          />
        </div>
        <div>
          <label htmlFor="term">Term:</label>
          <input
            type="text"
            id="term"
            value={term}
            onChange={(e) => setTerm(e.target.value)}
          />
        </div>
        <button onClick={handleFetchSchedule}>Fetch Schedule</button>
      </div>

      {error && <div className="schedule-error">Error: {error}</div>}

      {scheduleData ? (
        <div className="schedule-data">
          <h2>Schedule for {year}, Term {term}</h2>
          {scheduleData.length === 0 ? (
            <p className="no-courses-message">No courses found for this period.</p>
          ) : (
            scheduleData.map((course, index) => (
              <div key={`${course.courseNumber || course.courseId || ''}-${index}`} className="course-item">
                <h3>{course.courseName || course.name}</h3>
                <p><strong>Instructor:</strong> {course.teacherName || course.instructor || 'N/A'}</p>
                <p><strong>Credits:</strong> {course.credits ?? 'N/A'}</p>
                <p><strong>Schedule:</strong> {formatDay(course.dayOfWeek)} {course.formattedPeriods || ''}</p>
                <p><strong>Location:</strong> {course.classroom || 'N/A'}</p>
                {course.weeks && course.weeks.length > 0 && (
                  <p><strong>Weeks:</strong> {course.weeks.join(', ')}</p>
                )}
              </div>
            ))
          )}
        </div>
      ) : (
        !error && <p className="loading-message">No schedule data loaded yet. Click "Fetch Schedule".</p>
      )}
    </div>
  );
};

export default ScheduleView;
