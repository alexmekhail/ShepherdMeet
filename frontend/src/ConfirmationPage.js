import React, { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import './ConfirmationPage.css';

const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5209';

// Parse "yyyy-MM-dd" without timezone shifting
const parseDate = (dateStr) => {
  if (!dateStr) return null;
  const [year, month, day] = dateStr.split('-').map(Number);
  return new Date(year, month - 1, day);
};

const ConfirmationPage = ({ onBooked }) => {
  const location = useLocation();
  const navigate = useNavigate();
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState(null);

  const { availabilityId, name, location: meetingLocation, date, time } = location.state || {};

  const meetingDate = parseDate(date);
  const formattedDate = meetingDate
    ? meetingDate.toLocaleDateString('en-US', {
        weekday: 'long',
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      })
    : date;

  const handleConfirm = async () => {
    setConfirming(true);
    setError(null);
    try {
      // Save the appointment
      const apptRes = await fetch(`${API_URL}/appointments`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, location: meetingLocation, date, time }),
      });

      if (!apptRes.ok) {
        throw new Error('Failed to save appointment.');
      }

      // Remove the slot from availability so it can't be double-booked
      if (availabilityId) {
        await fetch(`${API_URL}/priestavailabilities/${availabilityId}`, {
          method: 'DELETE',
          credentials: 'include',
        });
      }

      onBooked?.();
      navigate('/confirmed', { state: { name, date: formattedDate, time }, replace: true });
    } catch (e) {
      console.error(e);
      setError('Something went wrong. Please try again.');
    } finally {
      setConfirming(false);
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
      {error && <p className="confirm-error">{error}</p>}
      <button className="confirm-btn" onClick={handleConfirm} disabled={confirming}>
        {confirming ? 'Confirming…' : 'Confirm Appointment'}
      </button>
      <button className="cancel-btn" onClick={() => navigate('/')} disabled={confirming}>
        Cancel
      </button>
    </div>
  );
};

export default ConfirmationPage;
