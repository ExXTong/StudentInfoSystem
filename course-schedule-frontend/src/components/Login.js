import React, { useState } from 'react';
import { login } from '../api'; // Import the login function
import './Login.css'; // Import Login.css

const Login = ({ handleLoginSuccess }) => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError(null); // Reset error before new attempt
    try {
      const response = await login(username, password);
      if (response.success === true) {
        // Assuming response.user might not contain password, we'll pass username and original password
        // The backend response for login should ideally include necessary user details (e.g., name, role)
        // For now, we'll construct userCredentials with what we have.
        const userCredentials = { 
          username: response.user?.username || username, // Prefer response.user.username if available
          password: password, // Original password as API requires it
          name: response.user?.name, 
          role: response.user?.role 
        };
        if (handleLoginSuccess) {
          handleLoginSuccess(response.token, userCredentials);
        }
      } else {
        // This path might not be reached if 'login' throws an error for non-ok responses.
        // Error handling is primarily in the catch block.
        setError(response.error?.message || 'Login failed. Please try again.');
      }
    } catch (apiError) {
      setError(apiError.error?.message || apiError.message || 'An unexpected error occurred.');
    }
  };

  return (
    <div className="login-container"> {/* Added login-container class */}
      <h2>Login</h2> {/* Added heading */}
      <form onSubmit={handleSubmit}>
        {error && <div className="login-error">{error}</div>} {/* Added login-error class */}
        <div>
          <label htmlFor="username">Username:</label>
        <input
          type="text"
          id="username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
      </div>
      <div>
        <label htmlFor="password">Password:</label>
        <input
          type="password"
          id="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
      </div>
      <button type="submit">Login</button>
      </form>
    </div>
  );
};

export default Login;
