import React, { useState, useEffect } from 'react';
import { getSchedule } from '../api'; // Import getSchedule
import './ScheduleView.css'; // Import ScheduleView.css

const ScheduleView = ({ authToken, currentUser }) => {
  const [year, setYear] = useState('2024-2025');
  const [term, setTerm] = useState('1');
  const [scheduleData, setScheduleData] = useState(null); // Stores array of courses
  const [error, setError] = useState(null); // For API call errors

  // Optional: Keep or remove this useEffect for prop logging
  // useEffect(() => {
  //   console.log('ScheduleView - authToken:', authToken);
  //   console.log('ScheduleView - currentUser:', currentUser);
  // }, [authToken, currentUser]);

  const handleFetchSchedule = async () => { // Make async
    setError(null); // Clear previous errors
    setScheduleData(null); // Clear previous schedule data

    if (!currentUser || !authToken) {
      setError("User not logged in or token missing.");
      return;
    }

    const { username, password } = currentUser; // Assuming password is required by API and stored in currentUser

    try {
      const response = await getSchedule(authToken, username, password, year, term);
      if (response && response.success && response.schedule && Array.isArray(response.schedule.courses)) {
        setScheduleData(response.schedule.courses);
      } else {
        setError(response?.error?.message || 'Failed to fetch schedule. Invalid data format.');
      }
    } catch (apiError) {
      setError(apiError.error?.message || apiError.message || 'An unexpected error occurred while fetching schedule.');
    }
  };

  return (
    <div className="schedule-view-container"> {/* Added schedule-view-container class */}
      {currentUser && (
        <div className="user-welcome"> {/* Added user-welcome class */}
          <p>Welcome, {currentUser.name || currentUser.username}!</p>
          {currentUser.role && <p>Role: {currentUser.role}</p>}
        </div>
      )}
      <div className="schedule-controls"> {/* Added schedule-controls class */}
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

      {error && <div className="schedule-error">Error: {error}</div>} {/* Added schedule-error class */}

      {scheduleData ? (
        <div className="schedule-data"> {/* Added schedule-data class */}
          <h2>Schedule for {year}, Term {term}</h2>
          {scheduleData.length === 0 ? (
            <p className="no-courses-message">No courses found for this period.</p> {/* Added no-courses-message class */}
          ) : (
            scheduleData.map((course, index) => (
              <div key={index} className="course-item"> {/* Added course-item class */}
                <h3>{course.name}</h3>
                <p><strong>Instructor:</strong> {course.instructor}</p>
                <p><strong>Credits:</strong> {course.credits}</p>
                <h4>Schedule:</h4>
                {course.schedule && course.schedule.length > 0 ? (
                  <ul>
                    {course.schedule.map((slot, sIndex) => (
                      <li key={sIndex}>
                        {slot.day}: {slot.startTime} - {slot.endTime} @ {slot.location}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p>No schedule slots defined for this course.</p>
                )}
              </div>
            ))
          )}
        </div>
      ) : (
        !error && <p className="loading-message">No schedule data loaded yet. Click "Fetch Schedule".</p> /* Added loading-message class */
      )}
    </div>
  );
};

export default ScheduleView;
