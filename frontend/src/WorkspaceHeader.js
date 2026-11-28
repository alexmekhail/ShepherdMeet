import { NavLink } from 'react-router-dom';
import { API_URL } from './schedule';
import { useState } from 'react';
import ThemeToggle from './ThemeToggle';
import churchLogo from './spsdlogo.png';

export default function WorkspaceHeader({ profile, onSignOut }) {
  const [error, setError] = useState(false);
  async function signOut() {
    setError(false);
    try {
      const response = await fetch(API_URL + '/logout', { method: 'POST', credentials: 'include' });
      if (!response.ok) throw new Error();
      onSignOut();
    } catch {
      setError(true);
    }
  }
  return (
    <header className="workspace-header">
      <div className="header-inner">
        <NavLink className="brand" to="/">
          <img className="brand-logo" src={churchLogo} width="40" height="40" alt="" />
          ShepherdMeet
        </NavLink>
        {profile && <nav aria-label="Main navigation">
          <NavLink to="/" end>Appointments</NavLink>
          <NavLink to={profile.isAdmin ? '/availability' : '/book'}>
            {profile.isAdmin ? 'Manage availability' : 'Book a meeting'}
          </NavLink>
        </nav>}
        <ThemeToggle />
        {profile && <div className="header-account">
          <div className="account-identity">
            <strong>{profile.firstName} {profile.lastName}</strong>
            <span>{profile.isAdmin ? 'Schedule administrator' : 'Parishioner'}</span>
          </div>
          <button type="button" className="secondary" onClick={signOut}>Sign out</button>
          {error && <p className="error" role="alert">Could not sign out. Try again.</p>}
        </div>}
      </div>
    </header>
  );
}
