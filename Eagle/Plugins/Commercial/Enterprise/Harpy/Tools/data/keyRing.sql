--
-- keyRing.sql --
--
-- Extensible Adaptable Generalized Logic Engine (Eagle)
-- Key Ring Database Schema
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
-- Create the base table for the identifier data.
--

CREATE TABLE IF NOT EXISTS Identifiers(
  Id TEXT PRIMARY KEY NOT NULL, -- Every row must have a unique GUID.
  Kind TEXT NOT NULL,           -- Always required, e.g. 'KeyPair'.
  Name TEXT NOT NULL,           -- Always required, e.g. 'EagleDemoPublic.snk'.
  [Group] TEXT NULL,            -- Optional title, e.g. 'Core Script Library'.
  Description TEXT NULL         -- Optional, 'Used to sign official scripts.'.
);

--
-- Create the base table for the key pair data.
--

CREATE TABLE IF NOT EXISTS KeyPairs(
  Id TEXT PRIMARY KEY NOT NULL, -- Required, e.g. '0x8bf43b4749e46a0b' (string).
  IdentifierId TEXT NOT NULL,   -- Required, reference to row in 'Identifiers'.
  Usage TEXT NOT NULL,          -- Always required, e.g. 'OLSQ' (flags).
  Expiration TEXT NULL,         -- Optional, e.g. '2017-12-06T05:37:20.9891935Z'.
  Domains TEXT NULL,            -- Optional, e.g. 'eagle.to' (list).
  Groups TEXT NULL,             -- Optional, e.g. '0x8bf43b4749e46a0b' (list).
  Bytes BLOB NOT NULL           -- Always required, actual key data.
);

--
-- Create the base table for the key ring data.
--

CREATE TABLE IF NOT EXISTS KeyRings(
  Id TEXT PRIMARY KEY NOT NULL, -- Required, e.g. '0x8bf43b4749e46a0b' (string).
  FileName TEXT NOT NULL,       -- Required, e.g. 'keyRing.enterprise.eagle'.
  DataBytes BLOB NOT NULL,      -- Required, file data (script bytes).
  SignatureBytes BLOB NOT NULL  -- Required, certificate data (xml bytes).
);

--
-- Create the view of keys for use by applications.
--

CREATE VIEW IF NOT EXISTS Keys AS
SELECT KeyPairs.Id AS KeyId,
       KeyPairs.IdentifierId AS IdentifierId,
       KeyPairs.Usage AS KeyUsage,
       KeyPairs.Expiration AS KeyExpiration,
       KeyPairs.Domains AS KeyDomains,
       KeyPairs.Groups AS KeyGroups,
       KeyPairs.Bytes AS KeyBytes,
       Identifiers.Kind AS IdentifierKind,
       Identifiers.Name AS IdentifierName,
       Identifiers.[Group] AS IdentifierGroup,
       Identifiers.Description AS IdentifierDescription
  FROM KeyPairs LEFT OUTER JOIN Identifiers
    ON KeyPairs.IdentifierId = Identifiers.Id;
