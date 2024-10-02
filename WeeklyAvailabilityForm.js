import React, { useState } from 'react';
import './WeeklyAvailabilityForm.css';

const WeeklyAvailabilityForm = () => {
    const [weeklyHours, setWeeklyHours] = useState({
        sunday: { available: false, startTime: '', endTime: '' },
        monday: { available: true, startTime: '09:00', endTime: '17:00' },
        tuesday: { available: true, startTime: '09:00', endTime: '17:00' },
        wednesday: { available: true, startTime: '09:00', endTime: '17:00' },
        thursday: { available: true, startTime: '09:00', endTime: '17:00' },
        friday: { available: true, startTime: '09:00', endTime: '17:00' },
        saturday: { available: false, startTime: '', endTime: '' },
    });

    const [dateRange, setDateRange] = useState({
        startDate: '',
        endDate: '',
        startTime: '09:00',
        endTime: '17:00',
        offDay: false,
    });

    const [specificDaysOff, setSpecificDaysOff] = useState([]);

    const handleDayChange = (day, field, value) => {
        setWeeklyHours(prev => ({
            ...prev,
            [day]: {
                ...prev[day],
                [field]: value,
            }
        }));
    };

    const handleAvailabilityToggle = (day) => {
        setWeeklyHours(prev => ({
            ...prev,
            [day]: {
                ...prev[day],
                available: !prev[day].available,
            }
        }));
    };

    const handleDateRangeChange = (e) => {
        const { name, value } = e.target;
        setDateRange(prev => ({
            ...prev,
            [name]: value,
        }));
    };

    const handleAddSpecificDaysOff = () => {
        if (dateRange.startDate && dateRange.endDate) {
            setSpecificDaysOff([...specificDaysOff, dateRange]);
            setDateRange({ startDate: '', endDate: '', startTime: '09:00', endTime: '17:00', offDay: false });
        }
    };

    const handleSubmitWeeklyHours = () => {
        // Get the current date
        const startDate = new Date();
        
        // Calculate the date 6 months from now
        const endDate = new Date();
        endDate.setMonth(startDate.getMonth() + 6);  // Add 6 months
    
        // Filter the available days and send individual requests for each day
        Object.keys(weeklyHours).forEach(day => {
            if (weeklyHours[day].available) {
                // Prepare the data for submission for each available day
                const availabilityData = {
                    userID: 1,  // Replace with dynamic userID if needed
                    startDate: startDate.toISOString(),  // Current date as start date
                    endDate: endDate.toISOString(),      // Date 6 months from now as end date
                    days: [day.charAt(0).toUpperCase() + day.slice(1)],  // Single day (e.g., ["Monday"])
                    startTime: weeklyHours[day].startTime + ':00',  // Start time with seconds
                    endTime: weeklyHours[day].endTime + ':00',      // End time with seconds
                    isAvailable: true,  // Availability status
                };
    
                console.log('Submitting for:', availabilityData);  // Log the data for debugging
    
                // Send the data to the server for each day
                fetch('http://localhost:5209/priestavailabilities', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify(availabilityData),
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Network response was not ok');
                    }
                    return response.json();
                })
                .then(data => {
                    console.log('Success:', data);
                })
                .catch(error => {
                    console.error('Error submitting weekly hours:', error);
                });
            }
        });
    };
    
    

    const handleSubmitSpecificDaysOff = () => {
        const specificDaysData = specificDaysOff.map(range => ({
            userID: 1,
            startdate: new Date(range.startDate).toISOString(),
            endDate: new Date(range.endDate).toISOString(),
            days: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
            startTime: '09:00:00',
            endTime: '17:00:00',
            isAvailable: false,
        }));

        specificDaysData.forEach(data => {
            console.log('Specific Day Off Data:', data); // Log for debugging

            fetch('http://localhost:5209/priestavailabilities', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(data),
            })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then(data => {
                console.log('Specific Day Off Success:', data);
            })
            .catch(error => {
                console.error('Error submitting specific days off:', error);
            });
        });
    };

    return (
        <div className="availability-container">
            <div className="availability-form">
                <h2>Weekly Hours</h2>
                {Object.keys(weeklyHours).map(day => (
                    <div key={day} className="day-row">
                        <label>
                            <input
                                type="checkbox"
                                checked={weeklyHours[day].available}
                                onChange={() => handleAvailabilityToggle(day)}
                            />
                            {day.toUpperCase()}
                        </label>
                        {weeklyHours[day].available && (
                            <>
                                <input
                                    type="time"
                                    value={weeklyHours[day].startTime}
                                    onChange={(e) => handleDayChange(day, 'startTime', e.target.value)}
                                />
                                <span>-</span>
                                <input
                                    type="time"
                                    value={weeklyHours[day].endTime}
                                    onChange={(e) => handleDayChange(day, 'endTime', e.target.value)}
                                />
                            </>
                        )}
                    </div>
                ))}
                <button className="save-button" onClick={handleSubmitWeeklyHours}>Save Weekly Hours</button>
            </div>

            <div className="specific-days-form">
                <h2>Specific Days Off</h2>
                <div className="date-range-picker">
                    <label>Start Date:</label>
                    <input
                        type="date"
                        name="startDate"
                        value={dateRange.startDate}
                        onChange={handleDateRangeChange}
                    />
                    <label>End Date:</label>
                    <input
                        type="date"
                        name="endDate"
                        value={dateRange.endDate}
                        onChange={handleDateRangeChange}
                    />
                    <label>
                        <input
                            type="checkbox"
                            checked={dateRange.offDay}
                            onChange={() => setDateRange(prev => ({ ...prev, offDay: !prev.offDay }))}
                        />
                        Off Day
                    </label>
                    <label>Start Time:</label>
                    <input
                        type="time"
                        name="startTime"
                        value={dateRange.startTime}
                        disabled={dateRange.offDay}
                        onChange={handleDateRangeChange}
                    />
                    <label>End Time:</label>
                    <input
                        type="time"
                        name="endTime"
                        value={dateRange.endTime}
                        disabled={dateRange.offDay}
                        onChange={handleDateRangeChange}
                    />
                    <button className="submit-date-range" onClick={handleAddSpecificDaysOff}>
                        Add Specific Days Off
                    </button>
                </div>

                {specificDaysOff.length > 0 && (
                    <div className="selected-date-range">
                        <h3>Selected Days Off:</h3>
                        <ul>
                            {specificDaysOff.map((range, index) => (
                                <li key={index}>
                                    {`${range.startDate} to ${range.endDate} (${range.startTime} - ${range.endTime})`}
                                </li>
                            ))}
                        </ul>
                        <button className="submit-specific-days" onClick={handleSubmitSpecificDaysOff}>
                            Submit Specific Days Off
                        </button>
                    </div>
                )}
            </div>
        </div>
    );
};

export default WeeklyAvailabilityForm;