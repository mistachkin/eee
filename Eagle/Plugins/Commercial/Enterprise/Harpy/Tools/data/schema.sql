--
-- schema.sql --
--
-- Extensible Adaptable Generalized Logic Engine (Eagle)
-- Kapok Database Schema
--
-- Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
--
-- See the file "license.terms" for information on usage and redistribution of
-- this file, and for a DISCLAIMER OF ALL WARRANTIES.
--
-- RCS: @(#) $Id: $
--
-------------------------------------------------------------------------------

--
-- Create the base table for the certificate data.  This should be usable
-- for all supported types of certificates (e.g. script, license, etc).
--

CREATE TABLE IF NOT EXISTS Certificates(
  Id TEXT PRIMARY KEY NOT NULL, -- Every row must have a unique GUID.
  Protocol TEXT NOT NULL,       -- Always required, enumeration value.
  ProtocolVersion TEXT NULL,    -- Not present for scripts.
  Vendor TEXT NULL,             -- Required for X.509 subject matching.
  Origin TEXT NULL,             -- For informational use only.
  Authority TEXT NULL,          -- Used for automatic renewal.
  Agreement TEXT NULL,          -- Matched to plugin license agreement.
  Support TEXT NULL,            -- Used for enterprise support portal.
  TimeStamp TEXT NOT NULL,      -- Signature DateTime for this certificate.
  Duration TEXT NOT NULL,       -- Always required, -1 for 'forever'.
  Key TEXT NOT NULL,            -- Public key token, required to verify.
  Number TEXT NULL,             -- Random number, absent for scripts.
  SerialNumber TEXT NULL,       -- Shared secret, absent for scripts.
  HashAlgorithm TEXT NULL,      -- Always required, default SHA1.
  Signature TEXT NOT NULL,      -- Base64 encoded bytes for RSA signature.
  Type TEXT NULL,               -- Not present for script certificates.
  EntityType TEXT NOT NULL,     -- Always required, enumeration value.
  EntityName TEXT NULL,         -- Always optional, name of target entity.
  EntityValue TEXT NULL,        -- Always optional, value of target entity.
  ExtraData TEXT NULL,          -- Always optional, auxiliary data.
  Quantity INTEGER NOT NULL,    -- May be zero, cannot be NULL.
  Product TEXT NULL,            -- Not present for scripts.
  Version TEXT NULL,            -- Not present for scripts.
  Features TEXT NULL,           -- Not present for scripts.
  Restrictions TEXT NULL,       -- Not present for scripts.
  GenerateNotes TEXT NULL,      -- Server use only, never signed.
  GenerateUserName TEXT NULL,   -- Server use only, never signed.
  GenerateFileName TEXT NULL,   -- Server use only, never signed.
  GenerateTimeStamp TEXT NULL,  -- Server use only, never signed.
  GenerateHostName TEXT NULL    -- Server use only, never signed.
);

--
-- Create the base table for the script data.  The script text for each
-- script certificate must be present in this table.
--

CREATE TABLE IF NOT EXISTS Scripts(
  Id TEXT PRIMARY KEY NOT NULL, -- Every row must have a unique GUID.
  CertificateId TEXT NOT NULL,  -- Associated script certificate Id.
  Text TEXT NOT NULL            -- The actual script text.
);

--
-- Create the base table for the client request data.
--

CREATE TABLE IF NOT EXISTS Requests(
  Id TEXT PRIMARY KEY NOT NULL, -- Every row must have a unique GUID.
  Type TEXT NOT NULL,           -- Always the literal string 'RENEW'.
  TimeStamp TEXT NOT NULL,      -- Received timestamp parameter value.
  CertificateId TEXT NULL,      -- Received certificate Id parameter value.
  Hash TEXT NOT NULL,           -- Received request hash parameter value.
  Status TEXT NULL              -- Status/error message for this request.
);

--
-- We need to be able to quickly lookup requests based on the Id of the
-- associated certificate; therefore, create a non-unique index (i.e. there
-- can be any number of requests submitted per certificate).
--

CREATE INDEX IF NOT EXISTS Requests_CertificateId
ON Requests (CertificateId ASC);

--
-- Create the base table for the subscription data.
--

CREATE TABLE IF NOT EXISTS Subscriptions(
  Id TEXT PRIMARY KEY NOT NULL, -- Every row must have a unique GUID.
  CertificateId TEXT NOT NULL,  -- The GUID of associated certificate.
  Duration TEXT NOT NULL,       -- Required, certificate renewal duration.
  Active TINYINT NOT NULL,      -- Non-zero if active and available.
  Expires TEXT NOT NULL         -- DateTime when subscription expires.
);

--
-- Create the base table for the support data.  The primary key for this table
-- is the SHA-512 hash of the associated certificate serial number.  The active
-- bit must be set for the support contract to be valid.  Also, the expiration
-- date must not have been passed yet.
--

CREATE TABLE IF NOT EXISTS Support(
  Id TEXT PRIMARY KEY NOT NULL, -- Every row must have a unique GUID.
  CertificateId TEXT NOT NULL,  -- The GUID of associated certificate.
  Active TINYINT NOT NULL,      -- Non-zero if active and available.
  Expires TEXT NOT NULL         -- DateTime when support contract expires.
);

--
-- We need to be able to quickly lookup a script based on the Id of the
-- associated certificate; therefore, create a unique index (i.e. there can
-- never be more than one script per certificate).
--

CREATE UNIQUE INDEX IF NOT EXISTS Scripts_CertificateId
ON Scripts (CertificateId ASC);

--
-- We need to be able to quickly lookup a subscription based on the Id of the
-- associated certificate; therefore, create a unique index (i.e. there can
-- never be more than one subscription per certificate).
--

CREATE UNIQUE INDEX IF NOT EXISTS Subscriptions_CertificateId
ON Subscriptions (CertificateId ASC);

--
-- We need to be able to quickly lookup a support contract based on the Id of
-- the associated certificate; therefore, create a unique index (i.e. there
-- can never be more than one support contract per certificate).
--

CREATE UNIQUE INDEX IF NOT EXISTS Support_CertificateId
ON Support (CertificateId ASC);
