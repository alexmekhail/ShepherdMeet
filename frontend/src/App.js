import React from 'react';
import { Routes, Route } from 'react-router-dom';
import PriestAvailabilityForm from './PriestAvailabilityForm';
import UserForm from './UserForm';
import ConfirmationPage from './ConfirmationPage';

const App = () => {
  return (
    <Routes>
      <Route path="/" element={<PriestAvailabilityForm />} />
      <Route path="/schedule" element={<UserForm />} />
      <Route path="/confirmation" element={<ConfirmationPage />} />
    </Routes>
  );
};

export default App;
