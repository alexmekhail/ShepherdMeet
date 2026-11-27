-- Run only on a disposable, migrated test database.
\set ON_ERROR_STOP on
BEGIN;
DO $$
DECLARE
    first_slot integer;
    overlap_slot integer;
    adjacent_slot integer;
BEGIN
    INSERT INTO "PriestAvailabilities"
        ("UserID", "StartDate", "EndDate", "Days", "StartTime", "EndTime", "IsAvailable")
    VALUES (0, '2099-01-01 10:00', '2099-01-01 10:30', ARRAY['Thursday'], '10:00', '10:30', true)
    RETURNING "ID" INTO first_slot;
    INSERT INTO "PriestAvailabilities"
        ("UserID", "StartDate", "EndDate", "Days", "StartTime", "EndTime", "IsAvailable")
    VALUES (0, '2099-01-01 10:15', '2099-01-01 10:45', ARRAY['Thursday'], '10:15', '10:45', true)
    RETURNING "ID" INTO overlap_slot;
    INSERT INTO "PriestAvailabilities"
        ("UserID", "StartDate", "EndDate", "Days", "StartTime", "EndTime", "IsAvailable")
    VALUES (0, '2099-01-01 10:30', '2099-01-01 11:00', ARRAY['Thursday'], '10:30', '11:00', true)
    RETURNING "ID" INTO adjacent_slot;

    INSERT INTO "Appointments"
        ("UserID", "Name", "Location", "Date", "Time", "CreatedAt", "AvailabilityID", "StartUtc", "EndUtc")
    VALUES (0, 'Fixture', '', '2099-01-01', '', now(), first_slot, '2099-01-01 18:00Z', '2099-01-01 18:30Z');

    BEGIN
        INSERT INTO "Appointments"
            ("UserID", "Name", "Location", "Date", "Time", "CreatedAt", "AvailabilityID", "StartUtc", "EndUtc")
        VALUES (0, 'Fixture', '', '2099-01-01', '', now(), overlap_slot, '2099-01-01 18:15Z', '2099-01-01 18:45Z');
        RAISE EXCEPTION 'PostgreSQL accepted overlapping bookings for different slot IDs';
    EXCEPTION WHEN exclusion_violation THEN
        NULL;
    END;

    BEGIN
        INSERT INTO "Appointments"
            ("UserID", "Name", "Location", "Date", "Time", "CreatedAt", "AvailabilityID", "StartUtc", "EndUtc")
        VALUES (0, 'Fixture', '', '2099-01-01', '', now(), adjacent_slot, '2099-01-01 18:30Z', '2099-01-01 19:01Z');
        RAISE EXCEPTION 'PostgreSQL accepted an invalid appointment duration';
    EXCEPTION WHEN check_violation THEN
        NULL;
    END;

    INSERT INTO "Appointments"
        ("UserID", "Name", "Location", "Date", "Time", "CreatedAt", "AvailabilityID", "StartUtc", "EndUtc")
    VALUES (0, 'Fixture', '', '2099-01-01', '', now(), adjacent_slot, '2099-01-01 18:30Z', '2099-01-01 19:00Z');
END $$;
ROLLBACK;
