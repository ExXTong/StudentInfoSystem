# Course Schedule Frontend

## Overview

This is a React-based frontend application designed to display university or school course schedules. It interacts with a backend API for user authentication (login) and for fetching detailed course schedule data. The application allows users to log in, specify a year and term, and view their course schedule, including course names, instructors, credits, and specific time slots with day, time, and location.

This project was bootstrapped with Vite.

## Features

*   User login using username (student ID/学号) and password.
*   Fetches and displays course schedules based on user-inputted year and term.
*   Shows comprehensive course details:
    *   Course Name
    *   Instructor
    *   Credits
    *   Specific time slots (Day, Start Time - End Time, Location)
*   Basic responsive styling for usability.
*   Error handling for API interactions and user inputs.

## Prerequisites

*   **Node.js and npm**: Node.js (e.g., v18.x or later recommended) and npm (Node Package Manager) must be installed. You can download them from [nodejs.org](https://nodejs.org/).
*   **Backend Service**: A running instance of the corresponding backend service. The frontend is configured to access this service at `https://localhost:10010/api`.
    *   **Important (Self-Signed SSL)**: If the backend uses a self-signed SSL certificate, you might need to manually instruct your web browser to accept this certificate. This usually involves visiting the backend URL directly (e.g., `https://localhost:10010`) and proceeding despite the browser's security warning.

## Setup and Installation

1.  **Clone the repository** (or ensure you have the project files):
    ```bash
    # If you are cloning a Git repository:
    # git clone <repository-url>
    # cd course-schedule-frontend
    ```
    If you have the files directly, navigate into the project's root directory:
    ```bash
    cd path/to/course-schedule-frontend
    ```

2.  **Install dependencies**:
    This command will download and install all the necessary packages defined in `package.json`.
    ```bash
    npm install
    ```

## Running the Application

1.  **Start the development server**:
    This command, provided by Vite, will start the local development server.
    ```bash
    npm run dev
    ```

2.  **Access the application**:
    Once the server is running, Vite will typically display a local URL in the terminal (e.g., `http://localhost:5173`). Open your web browser and navigate to this URL.

## Using the Application

1.  **Login**:
    *   On the login page, enter your student ID (学号) in the "Username" field.
    *   Enter your password in the "Password" field.
    *   Click the "Login" button.

2.  **View Schedule**:
    *   Upon successful login, you will be redirected to the schedule view page.
    *   Enter the desired academic `year` (e.g., "2024-2025").
    *   Enter the `term` number (e.g., "1" for the first term, "2" for the second).
    *   Click the "Fetch Schedule" button.
    *   Your course schedule for the specified year and term will be displayed below the input fields.

## Project Structure

The project follows a standard React application structure:

*   `public/`: Contains static assets that are served directly (e.g., `favicon.ico`, images).
*   `src/`: Contains the main source code for the application.
    *   `App.jsx`: The main application component that handles routing or conditional rendering of views.
    *   `main.jsx`: The entry point of the React application.
    *   `index.css`: Global styles for the application.
    *   `App.css`: Specific styles for the `App` component (can be minimal if component-specific CSS is used).
    *   `assets/`: Contains static assets like images or logos that are imported into components.
    *   `api.js`: A module dedicated to handling all communications with the backend API (login, fetching schedule).
    *   `components/`: Contains reusable React components:
        *   `Login.js`: The component responsible for the user login form and logic.
        *   `Login.css`: Styles specific to the `Login` component.
        *   `ScheduleView.js`: The component responsible for fetching and displaying the course schedule.
        *   `ScheduleView.css`: Styles specific to the `ScheduleView` component.
*   `package.json`: Lists project dependencies and scripts (like `npm run dev`).
*   `vite.config.js`: Configuration file for Vite.
*   `eslint.config.js`: Configuration for ESLint.

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules. For expanding ESLint configuration, refer to the official Vite documentation.
