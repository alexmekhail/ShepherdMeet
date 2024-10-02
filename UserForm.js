import React, { useState, useEffect } from 'react';
import './UserForm.css';
import { useNavigate } from 'react-router-dom';

const UserForm = () => {
  const [availabilities, setAvailabilities] = useState([]);
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [selectedMonth, setSelectedMonth] = useState(new Date().getMonth());
  const [selectedYear, setSelectedYear] = useState(new Date().getFullYear());
  const [selectedDay, setSelectedDay] = useState(null);
  const navigate = useNavigate();

  // Fetch availabilities
  const fetchAvailabilities = async () => {
    try {
      const response = await fetch('http://localhost:5209/priestavailabilities');
      if (response.ok) {
        const data = await response.json();
        setAvailabilities(data);
      } else {
        console.error('Error:', response.statusText);
      }
    } catch (error) {
      console.error('Error:', error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAvailabilities();
  }, []);

  const daysInMonth = (month, year) => new Date(year, month + 1, 0).getDate();

  const handleDayClick = (day) => {
    setSelectedDay(day);
  };

  const handlePrevMonth = () => {
    if (selectedMonth === 0) {
      setSelectedMonth(11);
      setSelectedYear(selectedYear - 1);
    } else {
      setSelectedMonth(selectedMonth - 1);
    }
  };

  const handleNextMonth = () => {
    if (selectedMonth === 11) {
      setSelectedMonth(0);
      setSelectedYear(selectedYear + 1);
    } else {
      setSelectedMonth(selectedMonth + 1);
    }
  };

  const formatTime = (timeString) => {
    if (!timeString) return ''; // Handle empty or undefined time strings
  
    const [hour, minute] = timeString.split(':');
    const hourInt = parseInt(hour, 10);
    const hour12 = hourInt % 12 || 12; // Convert 0 to 12 for midnight
    return `${hour12}:${minute}`;
  };
  

  const isAvailableDay = (day) => {
    return availabilities.some((availability) => {
      const availabilityDate = new Date(availability.startDate);
      return availabilityDate.getUTCDate() === day;
    });
  };

  const handleTimeSlotClick = (availability) => {
    navigate('/confirmation', {
      state: {
        name: 'Alex Mekhail',
        location: 'St. Philopater & St. Demiana Coptic Orthodox Church',
        date: `${selectedYear}-${selectedMonth + 1}-${selectedDay}`,
        time: `${formatTime(availability.startTime)} - ${formatTime(availability.endTime)}`,
      },
    });
  };

  // Get the day of the week for the first day of the month
  const firstDayOfMonth = new Date(selectedYear, selectedMonth, 1).getDay();

  return (
    <div className="boujee-container">
      <h2>Schedule a meeting with Father Danial</h2>
      {loading ? (
        <div className="loader">Loading...</div>
      ) : (
        <>
          <div className="calendar-header">
            <button className="month-nav" onClick={handlePrevMonth}>◀</button>
            <h3>{new Date(selectedYear, selectedMonth).toLocaleString('default', { month: 'long', year: 'numeric' })}</h3>
            <button className="month-nav" onClick={handleNextMonth}>▶</button>
          </div>

          <div className="weekdays">
            {['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'].map((day) => (
              <div key={day} className="weekday">
                {day}
              </div>
            ))}
          </div>

          <div className="calendar-grid">
            {Array.from({ length: firstDayOfMonth }).map((_, index) => (
              <div key={index} className="empty-day"></div>
            ))}

            {Array.from({ length: daysInMonth(selectedMonth, selectedYear) }, (_, day) => (
              <div
                key={day + 1}
                className={`calendar-day ${selectedDay === day + 1 ? 'selected' : ''} 
                ${isAvailableDay(day + 1) ? 'available' : ''}`}
                onClick={() => handleDayClick(day + 1)}
              >
                {day + 1}
              </div>
            ))}
          </div>

          {selectedDay && (
            <div className="availability-details">
              <h3>
                Available Appointments on {selectedDay}{' '}
                {new Date(selectedYear, selectedMonth).toLocaleString('default', { month: 'long' })}{' '}
                {selectedYear}
              </h3>
              <div className="time-slots">
                {availabilities
                  .filter((availability) => new Date(availability.startDate).getDate() === selectedDay)
                  .map((availability) => (
                    <button
                      key={availability.id}
                      className="time-slot-btn"
                      onClick={() => handleTimeSlotClick(availability)}
                    >
                      {formatTime(availability.startTime)} - {formatTime(availability.endTime)}
                    </button>
                  ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
};

export default UserForm;
