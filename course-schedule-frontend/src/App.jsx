import React, { useState } from 'react';
import Login from './components/Login';
import Dashboard from './components/Dashboard';
import './App.css';

function App() {
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [authToken, setAuthToken] = useState(null);
  const [currentUser, setCurrentUser] = useState(null);

  const handleLoginSuccess = (token, userCredentials) => {
    setAuthToken(token);
    setCurrentUser(userCredentials);
    setIsLoggedIn(true);
  };

  const handleLogout = () => {
    setAuthToken(null);
    setCurrentUser(null);
    setIsLoggedIn(false);
  };

  return (
    <div className="app-container">
      {isLoggedIn ? (
        <Dashboard
          authToken={authToken}
          currentUser={currentUser}
          onLogout={handleLogout}
        />
      ) : (
        <Login handleLoginSuccess={handleLoginSuccess} />
      )}
    </div>
  );
}

export default App;
