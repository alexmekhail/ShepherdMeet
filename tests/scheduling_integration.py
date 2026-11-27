"""HTTP integration tests against a migrated disposable PostgreSQL database."""

import concurrent.futures
import datetime as dt
import http.cookiejar
import json
import os
import subprocess
import threading
import unittest
import urllib.error
import urllib.request
from zoneinfo import ZoneInfo

BASE = os.environ.get("SHEPHERDMEET_TEST_URL", "http://localhost:5209/api")
SECOND = os.environ.get("SHEPHERDMEET_SECOND_TEST_URL", BASE)
ZONE = ZoneInfo("America/Los_Angeles")


class Client:
    def __init__(self, url=BASE):
        self.url = url
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar())
        )

    def request(self, method, path, payload=None, headers=None):
        data = None if payload is None else json.dumps(payload).encode()
        request = urllib.request.Request(
            self.url + path,
            data,
            {"Content-Type": "application/json", **(headers or {})},
            method=method,
        )
        try:
            response = self.opener.open(request, timeout=15)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            body = response.read()
            return response.code, json.loads(body) if body else None


class SchedulingTests(unittest.TestCase):
    def setUp(self):
        self.visitor = Client()
        self.parishioner = Client()
        self.admin = Client(SECOND)
        self.assertEqual(self.parishioner.request("POST", "/auth/demo-login", {"role": "parishioner"})[0], 200)
        self.assertEqual(self.admin.request("POST", "/auth/demo-login", {"role": "admin"})[0], 200)
        self.date = dt.datetime.now(ZONE).date() + dt.timedelta(days=50)

    def availability(self, start="18:00:00", end="18:30:00", available=True, date=None, last=None):
        day = date or self.date
        return {
            "startDate": day.isoformat(),
            "endDate": (last or day).isoformat(),
            "days": [day.strftime("%A")],
            "startTime": start,
            "endTime": end,
            "isAvailable": available,
        }

    def slot(self, date, start):
        status, slots = self.visitor.request("GET", "/priestavailabilities")
        self.assertEqual(status, 200)
        return next(item for item in slots if (item["startDate"], item["startTime"]) == (date.isoformat(), start))

    def db_sql(self, statement):
        return subprocess.check_output(
            ["psql", "-X", "--no-password", "-qAt", "-v", "ON_ERROR_STOP=1", "-c", statement],
            text=True,
        ).strip()

    @unittest.skipUnless(os.environ.get("PGDATABASE"), "Set PGDATABASE for disposable PostgreSQL fixtures")
    def test_previous_day_legacy_booking_blocks_midnight_only(self):
        day = self.date + dt.timedelta(days=(-self.date.weekday()) % 7)
        previous_day = day - dt.timedelta(days=1)
        appointment_id = int(self.db_sql(f'''
            INSERT INTO "Appointments" ("UserID", "Name", "Date", "Time", "Location", "CreatedAt")
            VALUES (0, 'Legacy fixture', '{previous_day}', '11:45 - 12:15', 'Test', NOW())
            RETURNING "ID"
        '''))
        self.addCleanup(self.db_sql, f'DELETE FROM "Appointments" WHERE "ID" = {appointment_id}')
        opening = self.availability("00:00:00", "01:00:00", date=day)
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", opening)[0], 204)
        midnight = self.slot(day, "00:00")
        self.assertTrue(midnight["isBooked"])
        self.assertFalse(self.slot(day, "00:30")["isBooked"])
        self.assertEqual(self.parishioner.request("POST", "/appointments", {"availabilityId": midnight["id"]})[0], 409)

    @unittest.skipUnless(os.environ.get("PGDATABASE"), "Set PGDATABASE for disposable PostgreSQL fixtures")
    def test_off_grid_slot_cannot_overlap_a_next_day_legacy_booking(self):
        day = self.date + dt.timedelta(days=(-self.date.weekday()) % 7 + 2)
        next_day = day + dt.timedelta(days=1)
        slot_id = int(self.db_sql(f'''
            INSERT INTO "PriestAvailabilities"
                ("UserID", "StartDate", "EndDate", "Days", "StartTime", "EndTime", "IsAvailable")
            VALUES (0, '{day} 23:45', '{next_day} 00:15', ARRAY['{day:%A}'], '23:45', '00:15', TRUE)
            RETURNING "ID"
        '''))
        self.addCleanup(self.db_sql, f'DELETE FROM "PriestAvailabilities" WHERE "ID" = {slot_id}')
        appointment_id = int(self.db_sql(f'''
            INSERT INTO "Appointments" ("UserID", "Name", "Date", "Time", "Location", "CreatedAt")
            VALUES (0, 'Legacy fixture', '{next_day}', '12:00 AM - 12:30 AM', 'Test', NOW())
            RETURNING "ID"
        '''))
        self.addCleanup(self.db_sql, f'DELETE FROM "Appointments" WHERE "ID" = {appointment_id}')
        self.assertTrue(self.slot(day, "23:45")["isBooked"])
        self.assertEqual(self.parishioner.request("POST", "/appointments", {"availabilityId": slot_id})[0], 409)

    @unittest.skipUnless(os.environ.get("PGDATABASE"), "Set PGDATABASE for disposable PostgreSQL fixtures")
    def test_opening_replaces_only_fully_covered_off_grid_legacy_block(self):
        block_id = int(self.db_sql(f'''
            INSERT INTO "PriestAvailabilities"
                ("UserID", "StartDate", "EndDate", "Days", "StartTime", "EndTime", "IsAvailable")
            VALUES (0, '{self.date} 10:15', '{self.date} 10:45', ARRAY['{self.date:%A}'], '10:15', '10:45', FALSE)
            RETURNING "ID"
        '''))
        self.addCleanup(self.db_sql, f'DELETE FROM "PriestAvailabilities" WHERE "ID" = {block_id}')

        partial = self.availability("10:00:00", "10:30:00")
        status, problem = self.admin.request("POST", "/priestavailabilities", partial)
        self.assertEqual(status, 409)
        self.assertIn("covers the whole block", problem["detail"])
        self.assertFalse(self.slot(self.date, "10:15")["isAvailable"])

        full = self.availability("10:00:00", "11:00:00")
        appointment_id = int(self.db_sql(f'''
            INSERT INTO "Appointments"
                ("UserID", "Name", "Date", "Time", "Location", "CreatedAt", "AvailabilityID", "StartUtc", "EndUtc")
            SELECT 0, 'Linked fixture', '{self.date}', '10:15 AM - 10:45 AM', 'Test', NOW(), "ID",
                "StartDate" AT TIME ZONE 'America/Los_Angeles', "EndDate" AT TIME ZONE 'America/Los_Angeles'
            FROM "PriestAvailabilities" WHERE "ID" = {block_id}
            RETURNING "ID"
        '''))
        remove_appointment = f'DELETE FROM "Appointments" WHERE "ID" = {appointment_id}'
        self.addCleanup(self.db_sql, remove_appointment)
        status, problem = self.admin.request("POST", "/priestavailabilities", full)
        self.assertEqual(status, 409)
        self.assertIn("linked to an appointment", problem["detail"])
        self.assertFalse(self.slot(self.date, "10:15")["isAvailable"])

        self.db_sql(remove_appointment)
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", full)[0], 204)
        self.assertTrue(self.slot(self.date, "10:00")["isAvailable"])
        self.assertTrue(self.slot(self.date, "10:30")["isAvailable"])
        self.assertEqual(self.db_sql(f'SELECT COUNT(*) FROM "PriestAvailabilities" WHERE "ID" = {block_id}'), "0")

    def test_concurrent_booking_ownership_cancellation_and_rebooking(self):
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", self.availability())[0], 204)
        slot = self.slot(self.date, "18:00")
        payload = {"availabilityId": slot["id"], "date": "1900-01-01", "name": "Client input"}
        barrier = threading.Barrier(2)

        def book(client):
            barrier.wait(timeout=10)
            return client.request("POST", "/appointments", payload)

        clients = [self.parishioner, self.admin]
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            results = list(executor.map(book, clients))

        self.assertEqual(sorted(status for status, _ in results), [201, 409])
        winner = next(i for i, (status, _) in enumerate(results) if status == 201)
        owner, other = clients[winner], clients[1 - winner]
        appointment = results[winner][1]
        queued_start = dt.datetime.combine(self.date, dt.time(18), ZONE).astimezone(dt.timezone.utc)
        self.assertEqual(dt.datetime.fromisoformat(appointment["startUtc"]), queued_start)
        self.assertEqual(appointment["date"], self.date.isoformat())
        self.assertNotEqual(appointment["name"], "Client input")
        booked = self.slot(self.date, "18:00")
        self.assertTrue(booked["isBooked"])
        self.assertEqual(set(booked), {"id", "startDate", "startTime", "endTime", "isAvailable", "isBooked"})
        route = f"/appointments/{appointment['id']}"
        self.assertNotIn(appointment["id"], [row["id"] for row in other.request("GET", "/appointments")[1]])
        self.assertEqual(other.request("DELETE", route)[0], 404)
        self.assertEqual(self.visitor.request("DELETE", route)[0], 401)
        self.assertEqual(owner.request("DELETE", route)[0], 204)
        status, rebooked = other.request("POST", "/appointments", payload)
        self.assertEqual(status, 201)

        # Cancellation preserves an administrator's later override.
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", self.availability(available=False))[0], 204)
        self.assertEqual(other.request("DELETE", f"/appointments/{rebooked['id']}")[0], 204)
        self.assertEqual(owner.request("POST", "/appointments", payload)[0], 409)

    def test_weekly_expansion_and_date_override(self):
        last = self.date + dt.timedelta(days=14)
        weekly = self.availability("19:00:00", "20:00:00", last=last)
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", weekly)[0], 204)
        for offset in (0, 7, 14):
            occurrence = self.date + dt.timedelta(days=offset)
            self.assertTrue(self.slot(occurrence, "19:30")["isAvailable"])
        middle = self.date + dt.timedelta(days=7)
        override = self.availability("19:00:00", "19:30:00", False, middle)
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", override)[0], 204)
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", weekly)[0], 204)
        self.assertFalse(self.slot(middle, "19:00")["isAvailable"])
        self.assertTrue(self.slot(middle, "19:30")["isAvailable"])
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", {**override, "isAvailable": True})[0], 204)
        self.assertTrue(self.slot(middle, "19:00")["isAvailable"])

    def test_account_management_denies_non_account_admins(self):
        self.assertTrue(self.admin.request("GET", "/profile")[1]["isAdmin"])
        operations = [
            ("GET", "/users", None),
            ("PUT", "/users/1", {"firstName": "Unauthorized"}),
            ("POST", "/users/cancel", None),
        ]
        for method, path, body in operations:
            with self.subTest(method=method, path=path):
                self.assertEqual(self.visitor.request(method, path, body)[0], 401)
                self.assertEqual(self.parishioner.request(method, path, body)[0], 403)
                self.assertEqual(self.admin.request(method, path, body)[0], 403)

    def test_invalid_requests_and_permissions(self):
        self.assertEqual(self.visitor.request("GET", "/profile")[0], 401)
        self.assertEqual(self.visitor.request("GET", "/appointments")[0], 401)
        self.assertEqual(self.visitor.request("POST", "/appointments", {"availabilityId": 1})[0], 401)
        self.assertEqual(self.parishioner.request("POST", "/appointments", {"availabilityId": 0})[0], 400)
        status, problem = self.parishioner.request("POST", "/appointments", {"availabilityId": 2147483647})
        self.assertEqual(status, 409)
        self.assertIn("no longer available", problem["detail"])
        availability = self.availability()
        self.assertEqual(self.visitor.request("POST", "/priestavailabilities", availability)[0], 401)
        self.assertEqual(self.parishioner.request("POST", "/priestavailabilities", availability)[0], 403)
        self.assertEqual(self.parishioner.request("DELETE", "/priestavailabilities/1")[0], 403)
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", availability,
                                           {"Origin": "https://untrusted.example"})[0], 403)
        for start, end in (("18:00:00", "18:15:00"), ("18:30:00", "18:00:00"), ("18:00:00", "2.00:00:00")):
            self.assertEqual(self.admin.request("POST", "/priestavailabilities", self.availability(start, end))[0], 400)

    def test_daylight_saving_and_midnight(self):
        year = self.date.year + 1
        march = dt.date(year, 3, 8)
        spring = march + dt.timedelta(days=(6 - march.weekday()) % 7)
        november = dt.date(year, 11, 1)
        fall = november + dt.timedelta(days=(6 - november.weekday()) % 7)
        for day, start, end in ((spring, "02:00:00", "02:30:00"), (fall, "01:00:00", "01:30:00")):
            status, problem = self.admin.request("POST", "/priestavailabilities", self.availability(start, end, date=day))
            self.assertEqual(status, 400)
            self.assertIn("daylight-saving", problem["detail"])

        day = self.date + dt.timedelta(days=1)
        midnight = self.availability("23:30:00", "1.00:00:00", date=day)
        self.assertEqual(self.admin.request("POST", "/priestavailabilities", midnight)[0], 204)
        slot = self.slot(day, "23:30")
        self.assertEqual(slot["endTime"], "00:00")
        status, appointment = self.parishioner.request("POST", "/appointments", {"availabilityId": slot["id"]})
        self.assertEqual(status, 201)
        end = dt.datetime.fromisoformat(appointment["endUtc"]).astimezone(ZONE)
        self.assertEqual(end.date(), day + dt.timedelta(days=1))
        self.assertEqual(end.time(), dt.time.min)
        self.assertEqual(self.parishioner.request("DELETE", f"/appointments/{appointment['id']}")[0], 204)


if __name__ == "__main__":
    unittest.main(verbosity=2)
