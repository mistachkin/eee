--
-- var.sql --
--
-- Extensible Adaptable Generalized Logic Engine (Eagle)
-- Variable Storage Server Database Schema
--
-- Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
--
-- See the file "license.terms" for information on usage and redistribution of
-- this file, and for a DISCLAIMER OF ALL WARRANTIES.
--
-- RCS: @(#) $Id: $
--
-------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS vars(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL UNIQUE,
  value TEXT NULL
);

CREATE TABLE IF NOT EXISTS apiKeys(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  apiKey TEXT NOT NULL UNIQUE,
  active INTEGER NOT NULL
);

PRAGMA foreign_keys = ON;
