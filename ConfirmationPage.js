// ConfirmationPage.js
import React from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import './ConfirmationPage.css';

const ConfirmationPage = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const { name, location: meetingLocation, date, time } = location.state || {};
  const meetingDate = new Date(date);

  const formattedDate = meetingDate.toLocaleDateString('en-US', {
    weekday: 'long',  // Day of the week (e.g., Monday)
    year: 'numeric',  // Full year (e.g., 2023)
    month: 'long',    // Full month name (e.g., September)
    day: 'numeric',   // Day of the month (e.g., 26)
  });

  const handleConfirm = async () => {
    // Send confirmation to the backend (this is just a placeholder)
    try {
      const response = await fetch('http://localhost:5209/appointments', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          name,
          location: meetingLocation,
          date,
          time,
        }),
      });

      if (response.ok) {
        alert('Appointment confirmed!');
        navigate('/'); // Redirect to home after confirmation
      } else {
        alert('Appointment confirmed!');
      }
    } catch (error) {
      console.error('Error confirming appointment:', error);
    }
  };

  return (
    <div className="confirmation-container">
      <h2>Confirm Appointment</h2>
      <div className="appointment-details">
        <p><strong>Name:</strong> {name}</p>
        <p><strong>Location:</strong> {meetingLocation}</p>
        <p><strong>Date:</strong> {formattedDate}</p>
        <p><strong>Time:</strong> {time}</p>
      </div>
      <button className="confirm-btn" onClick={handleConfirm}>Confirm Appointment</button>
    </div>
  );
};

export default ConfirmationPage;
