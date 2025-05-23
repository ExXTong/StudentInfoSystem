import React, { useState } from 'react';
import Login from './components/Login';
import ScheduleView from './components/ScheduleView';
import './App.css';

function App() {
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [authToken, setAuthToken] = useState(null); // Add authToken state
  const [currentUser, setCurrentUser] = useState(null); // Add currentUser state

  // Function to handle successful login
  const handleLoginSuccess = (token, userCredentials) => { // Accept token and userCredentials
    setAuthToken(token); // Set authToken
    setCurrentUser(userCredentials); // Set currentUser
    setIsLoggedIn(true); // Set isLoggedIn to true
  };

  return (
    <div className="app-container"> {/* Added app-container class */}
      {isLoggedIn ? (
        <ScheduleView authToken={authToken} currentUser={currentUser} /> // Pass authToken and currentUser
      ) : (
        <Login handleLoginSuccess={handleLoginSuccess} />
      )}
    </div>
  );
}

export default App;
