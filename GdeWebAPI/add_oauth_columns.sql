-- SQL script az OAuth oszlopok hozzáadásához
-- Futtatás: sqlite3 gde.db < add_oauth_columns.sql
-- VAGY: Használjon egy SQLite böngészőt (pl. DB Browser for SQLite)

-- OAuth oszlopok hozzáadása a T_USER táblához
ALTER TABLE T_USER ADD COLUMN OAUTHPROVIDER TEXT NULL;
ALTER TABLE T_USER ADD COLUMN OAUTHID TEXT NULL;
ALTER TABLE T_USER ADD COLUMN PROFILEPICTURE TEXT NULL;
ALTER TABLE T_USER ADD COLUMN ONBOARDINGCOMPLETED INTEGER NOT NULL DEFAULT 0;

