import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import UserForm from './UserForm';
import ConfirmationPage from './ConfirmationPage';
import AppointmentConfirmed from './AppointmentConfirmed';
import { churchToday, slotLabel } from './schedule';

const profile = { timeZone: 'America/Los_Angeles' };
const today = churchToday(profile.timeZone);
const slot = { id: 42, startDate: today, startTime: '13:00', endTime: '13:30', isAvailable: true };

test('dates and slot labels use the church date and explicit AM/PM', () => {
  expect(churchToday(profile.timeZone, new Date('2027-01-01T03:30:00Z'))).toBe('2026-12-31');
  expect(slotLabel({ startTime: '23:30', endTime: '00:00' })).toBe('11:30 PM – 12:00 AM');
});

describe('booking calendar', () => {
  let container;
  let root;
  const originalFetch = global.fetch;

  beforeEach(() => {
    global.IS_REACT_ACT_ENVIRONMENT = true;
    container = document.createElement('div');
    document.body.append(container);
    root = createRoot(container);
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    global.fetch = originalFetch;
    delete global.IS_REACT_ACT_ENVIRONMENT;
  });

  function button(text) {
    return Array.from(container.querySelectorAll('button')).find((node) => node.textContent.includes(text));
  }

  async function openCalendar(status) {
    global.fetch = jest.fn()
      .mockResolvedValueOnce({ ok: true, json: async () => [slot, { ...slot, id: 43, isBooked: true }] })
      .mockResolvedValueOnce({
        ok: status === 201,
        status,
        json: async () => status === 201
          ? { id: 100, date: today, time: slotLabel(slot), location: 'Church' }
          : { detail: 'This time is no longer available.' },
      });
    await act(async () => root.render(
      <MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <Routes>
          <Route path="/" element={<UserForm profile={profile} />} />
          <Route path="/confirmation" element={<ConfirmationPage profile={profile} />} />
          <Route path="/confirmed" element={<AppointmentConfirmed />} />
        </Routes>
      </MemoryRouter>
    ));
    expect(button('Review appointment').disabled).toBe(true);
    await act(async () => container.querySelector('[aria-label$=", times available"]').click());
    expect(button('Booked').disabled).toBe(true);
    await act(async () => button('1:00 PM').click());
    expect(button('1:00 PM').getAttribute('aria-pressed')).toBe('true');
    await act(async () => button('Review appointment').click());
    await act(async () => button('Confirm appointment').click());
  }

  test('submits only the slot identity and displays confirmation', async () => {
    await openCalendar(201);
    expect(JSON.parse(global.fetch.mock.calls[1][1].body)).toEqual({ availabilityId: 42 });
    expect(global.fetch.mock.calls[1][1].credentials).toBe('include');
    expect(container.querySelector('h1').textContent).toBe('Appointment confirmed');
  });

  test('shows a booking conflict and lets the user return to the calendar', async () => {
    await openCalendar(409);
    expect(container.querySelector('[role="alert"]').textContent).toContain('no longer available');
    expect(container.querySelector('a').textContent).toBe('Back to calendar');
  });
});
