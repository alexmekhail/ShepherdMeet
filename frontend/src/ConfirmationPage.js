import React from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import './ConfirmationPage.css';

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5209';

const ConfirmationPage = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const { name, location: meetingLocation, date, time } = location.state || {};
  const meetingDate = new Date(date);

  const formattedDate = meetingDate.toLocaleDateString('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });

  const handleConfirm = async () => {
    try {
      const response = await fetch(`${API_URL}/appointments`, {
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
        navigate('/');
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
