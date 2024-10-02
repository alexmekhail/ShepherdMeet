import React, { useState, useEffect } from 'react';
import './PriestAvailabilityForm.css';

const PriestAvailabilityForm = () => {
  const [availabilities, setAvailabilities] = useState([]);
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [selectedMonth, setSelectedMonth] = useState(new Date().getMonth());
  const [selectedYear, setSelectedYear] = useState(new Date().getFullYear());
  const [selectedDay, setSelectedDay] = useState(null);
  const [editingAvailability, setEditingAvailability] = useState(null);
  const [newAvailability, setNewAvailability] = useState({
    startDate: '',
    endDate: '',
    days: [],
    startTime: '',
    endTime: '',
    isAvailable: false,
  });

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

  // Fetch profile
  const fetchProfile = async () => {
    try {
      const response = await fetch('http://localhost:5209/profile');
      if (response.ok) {
        const data = await response.json();
        setProfile(data);
      } else {
        console.error('Error:', response.statusText);
      }
    } catch (error) {
      console.error('Error:', error);
    }
  };

  useEffect(() => {
    fetchAvailabilities();
    fetchProfile();
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

  const handleAddAvailability = async () => {
    // API logic to add new availability
    try {
      const response = await fetch('http://localhost:5209/priestavailabilities', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(newAvailability),
      });
      if (response.ok) {
        alert('Availability added successfully!');
        fetchAvailabilities();
      }
    } catch (error) {
      console.error('Error adding availability:', error);
    }
    resetForm();
  };

  const handleUpdateAvailability = async () => {
    // API logic to update the availability
    try {
      const response = await fetch(`http://localhost:5209/priestavailabilities/${editingAvailability.id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(newAvailability),
      });
      if (response.ok) {
        alert('Availability updated successfully!');
        fetchAvailabilities();
      }
    } catch (error) {
      console.error('Error updating availability:', error);
    }
    resetForm();
  };

  const resetForm = () => {
    setNewAvailability({
      startDate: '',
      endDate: '',
      days: [],
      startTime: '',
      endTime: '',
      isAvailable: false,
    });
    setEditingAvailability(null);
  };

  const handleInputChange = (e) => {
    const { name, value } = e.target;
  
    if (name === 'startDate') {
      setNewAvailability((prev) => ({
        ...prev,
        [name]: value,
        endDate: prev.endDate === '' || prev.endDate === prev.startDate ? value : prev.endDate, // Update endDate only if it's empty or the same as startDate
      }));
    } else {
      setNewAvailability({
        ...newAvailability,
        [name]: value,
      });
    }
  };

  const handleDaysChange = (e) => {
    const { value, checked } = e.target;
    const updatedDays = checked
      ? [...newAvailability.days, value]
      : newAvailability.days.filter((day) => day !== value);

    setNewAvailability({ ...newAvailability, days: updatedDays });
  };

  const handleEditClick = (availability) => {
    setEditingAvailability(availability);
    setNewAvailability({
      startDate: availability.startDate,
      endDate: availability.endDate,
      days: availability.days,
      startTime: availability.startTime,
      endTime: availability.endTime,
      isAvailable: availability.isAvailable,
    });
  };

  const isAvailableDay = (day) => {
    return availabilities.some(availability => 
      new Date(availability.startDate).getDate() === day && availability.isAvailable
    );
  };
  
  const isUnavailableDay = (day) => {
    return !availabilities.some(availability => 
      new Date(availability.startDate).getDate() === day
    );
  };
  

  return (
    <div className="boujee-container">
      <h2>Priest Availabilities</h2>
      {loading ? (
        <div className="loader">Loading...</div>
      ) : (
        <>
          {profile && profile.role === 'Admin' && (
            <div className="admin-panel">
              <h3>Welcome, {profile.firstname} {profile.lastname}</h3>
              <button className="month-nav" onClick={() => resetForm()}>Add Availability</button>
            </div>
          )}
          <div className="calendar-header">
            <button className="month-nav" onClick={handlePrevMonth}>◀</button>
            <h3>{new Date(selectedYear, selectedMonth).toLocaleString('default', { month: 'long', year: 'numeric' })}</h3>
            <button className="month-nav" onClick={handleNextMonth}>▶</button>
          </div>
          <div className="calendar-grid">
            {Array.from({ length: daysInMonth(selectedMonth, selectedYear) }, (_, day) => (
              <div
                key={day + 1}
                className={`calendar-day ${selectedDay === day + 1 ? 'selected' : ''} 
                ${isAvailableDay(day + 1) ? 'available' : ''} 
                ${isUnavailableDay(day + 1) ? 'unavailable' : ''}`}
                onClick={() => handleDayClick(day + 1)}
              >
                {day + 1}
              </div>
            ))}
          </div>
  
          {selectedDay && (
            <div className="availability-details">
              <h3>Availability for {selectedDay} {new Date(selectedYear, selectedMonth).toLocaleString('default', { month: 'long' })} {selectedYear}</h3>
              <ul>
                {availabilities
                  .filter(availability => new Date(availability.startDate).getDate() === selectedDay)
                  .map((availability) => (
                    <li key={availability.id}>
                      <strong>Priest {availability.userID}</strong>:<br />
                      {availability.startTime} - {availability.endTime}<br />
                      
                    </li>
                  ))}
              </ul>
            </div>
          )}
  
          <div className="edit-availability">
            <h3>Edit Availability for {selectedDay} {new Date(selectedYear, selectedMonth).toLocaleString('default', { month: 'long' })} {selectedYear}</h3>
            <form onSubmit={(e) => { e.preventDefault(); editingAvailability ? handleUpdateAvailability() : handleAddAvailability(); }}>
              <label>Start Date</label>
              <input 
                type="date" 
                name="startDate" 
                value={newAvailability.startDate || `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(selectedDay).padStart(2, '0')}`} 
                onChange={handleInputChange} 
              />
              <label>End Date</label>
              <input 
                type="date" 
                name="endDate" 
                value={newAvailability.endDate || `${selectedYear}-${String(selectedMonth + 1).padStart(2, '0')}-${String(selectedDay).padStart(2, '0')}`} 
                onChange={handleInputChange} 
              />
              <label>Start Time</label>
              <input type="time" name="startTime" value={newAvailability.startTime} onChange={handleInputChange} />
              <label>End Time</label>
              <input type="time" name="endTime" value={newAvailability.endTime} onChange={handleInputChange} />
              <label>Available?</label>
              <input
                type="checkbox"
                name="isAvailable"
                checked={newAvailability.isAvailable}
                onChange={(e) => setNewAvailability({ ...newAvailability, isAvailable: e.target.checked })}
              />
              <button type="submit">{editingAvailability ? 'Update Availability' : 'Add Availability'}</button>
            </form>
          </div>
        </>
      )}
    </div>
    
  );
                  };

  export default PriestAvailabilityForm;