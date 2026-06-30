--
-- test.sql --
--
-- Extensible Adaptable Generalized Logic Engine (Eagle)
-- Kapok Sample Data
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
-- Insert our "sample" license certificate data.
--

INSERT OR REPLACE INTO Certificates (
  Id, Protocol, ProtocolVersion, Vendor, Origin, Authority, Agreement,
  Support, TimeStamp, Duration, Key, Number, SerialNumber, HashAlgorithm,
  Signature, Type, EntityType, EntityName, EntityValue, ExtraData,
  Quantity, Product, Version, Features, Restrictions
) VALUES (
  'f1aa1901-3384-4d85-aecd-c8f9992e175f', 'Local', '4.0',
  'Eagle Development Team', NULL, NULL,
  'https://eagle.to/standard/license.html',
  'https://eagle.to/standard/support.html', '2009-11-22T10:11:06.4995000Z',
  '-1.00:00:00', '0x8bf43b4749e46a0b', NULL, 'ESE-001-000', 'SHA512', '
CaDcrTR0I3eLsPZ33dEQFvWZ1C4Q5opO5hcdR9hqZz8GPgjZICZh1Hsm1M3ga2QNOwzdUxFylKlA
n0Rs4qhVbwTWeJqXJyW0l7/kgut4mz1REKhQCi6D0RUE/ohs1xMQXapkA4OIsid7PUKO9dckn3bB
tcUlZUDItAzMzKeDxyqJRDwMBK/4i3zISPJRiO3zIJ/AoyjgJynx+oawI5O64X/mUjnaILnQnmov
rNeLtscz/3kgl03xHAIDlOT37qn9YstQEN9oRQGfpXwj7maOol05HHd1C9kndFG73hTgCXVd0T6y
kUZ9FYjv+KVzTTYLq6eijA4TP2w1UZTKEl9Sy5x0cKQz12iq5OJjMw6s568eC7ciOXVt1z+TSTnL
DF093q3PGr6QhQipbvUyDV6iPRAdst7m4UWdmp5GWHX+GtNGXB6RVevOPmG1VrvhZAIg3iOOn7dl
7MAunmCdD6vT95aqndCgG39AgPKWSWd25ZCQgWqmMxjh/z+wpsGcGpXnP5FPCOAky8d0K2L7TXqH
cu4XThECJIKcOuDvMs8RbScTmCkzjknKB15KlwZEfOaPNV62hJN/SmXWfW2kgq0ILP+EsfZ0Gf47
j/ZNgc8wI2OTIbsgXqtwiKzEM8p80q+doh45QdCJ0l++swY3GPhcTbBlZNkjiisMVZTDXktO0Og=',
  'Single User', 'Team', 'Eagle Development Team', NULL, NULL, 1,
  'Eagle Standard Edition', '1.0', NULL, NULL
);

INSERT OR REPLACE INTO Certificates (
  Id, Protocol, ProtocolVersion, Vendor, Origin, Authority, Agreement,
  Support, TimeStamp, Duration, Key, Number, SerialNumber, HashAlgorithm,
  Signature, Type, EntityType, EntityName, EntityValue, ExtraData,
  Quantity, Product, Version, Features, Restrictions
) VALUES (
  '1b40f0fe-a2fc-4c42-a644-45a851e1c75d', 'Local', '4.0',
  'Eagle Development Team', NULL, NULL,
  'https://eagle.to/enterprise/license.html',
  'https://eagle.to/enterprise/support.html', '2009-11-22T09:45:09.0307500Z',
  '-1.00:00:00', '0x8bf43b4749e46a0b', '0x462dac418c93cd81', 'EEE-001-000',
  'SHA512', '
d5On+Cvw0Syf6gm1mnXxja4O+K6qjQU7Hbh5eLq+Ex4xnCSYyjHwGu9IAY79tpDJvCHJm+PK1sUE
xZxo+Jea1/G7IRM4bd0cvqUlMII3Ad7Or1KR8oBYaaRBiCEKoPN/zJaPxH3nl8KeReU2lMCtB3s6
tG8/IoxVmOdMVF7qbRXwsPIan6FAbab4daBlFSszstIfYQ65ckzCmfIzmJBx8VRPwyRNrF9ijdGC
p78ejWD+8ex45id6FgZ5ULVCIXpf7bIl5Xi/M6Rdcrw2uHzhOVVc+/H03V8lgAon4JXprH3BkrTm
bTRG3NgFxv5joYcsTzFzewLlp1WBWNukjKTFFTdD9+SNWsClgawDaTcYh3rCwrHg2zlvg/YD+bxp
Nffn7pLj14Oax9dzPaDQx9KCaQ0g4HHns/GbBZJTGpsfZwga00uZQXdG/f6BXkym2jkyue4y0dCE
x0PIdaFp2YKGTq6BE5hv7/Q4t45IY/wOxmaoTqOsRA5ifAfi+HsSW+ZAnXb0mFOSF7B6Ai4YICv7
C6HvzetvSvyRLlMuciFoTCRycdneTKCqbTu+8vB1R18CmkCKZhc6V/hU3DBDp6KnBdM8tMn/C0vv
Ht4xC2V/IlK4OWyuqN89xysLwxZJgiPTjhXdn/XJixnJ9Et/2Cdvi1AuICWyMk/GNrjZ+WAnNAg=',
  'Single User', 'Team', 'Eagle Development Team', NULL, NULL, 1,
  'Eagle Enterprise Edition', '1.0', 'QXR', NULL
);

--
-- Insert our "sample" script certificate data.
--

INSERT INTO Certificates (
  Id, Protocol, ProtocolVersion, Vendor, Origin, Authority, Agreement,
  Support, TimeStamp, Duration, Key, Number, SerialNumber, HashAlgorithm,
  Signature, Type, EntityType, EntityName, EntityValue, ExtraData,
  Quantity, Product, Version, Features, Restrictions
) VALUES (
  '7e9ea823-6f37-484a-917f-ebe4efde7e3b', 'None', NULL, 'Mistachkin Systems',
  NULL, NULL, NULL, NULL, '2007-10-01T16:00:00.1122751Z', '7.00:00:00',
  '0x9559f6017247e3e2', NULL, NULL, 'SHA512', 'BAD_SIGNATURE', NULL, 'Script',
  NULL, NULL, NULL, 0, NULL, NULL, NULL, NULL
);

INSERT INTO Certificates (
  Id, Protocol, ProtocolVersion, Vendor, Origin, Authority, Agreement,
  Support, TimeStamp, Duration, Key, Number, SerialNumber, HashAlgorithm,
  Signature, Type, EntityType, EntityName, EntityValue, ExtraData,
  Quantity, Product, Version, Features, Restrictions
) VALUES (
  'd15f3455-1032-4b56-a2d2-b29dcddbceb7', 'None', NULL, 'Mistachkin Systems',
  NULL, NULL, NULL, NULL, '2007-10-01T16:00:00.1122751Z', '7.00:00:00',
  '0x9559f6017247e3e2', NULL, NULL, 'SHA512', 'BAD_SIGNATURE', NULL, 'Script',
  NULL, NULL, NULL, 0, NULL, NULL, NULL, NULL
);

--
-- Insert our "sample" script data.
--

INSERT OR REPLACE INTO Scripts (
  Id, CertificateId, Text
) VALUES (
  '10983482-5c19-4230-8e28-852fa9480624',
  '7e9ea823-6f37-484a-917f-ebe4efde7e3b',
  '' -- NOT NULL, Replace with "secureTest8.eagle" content.
);

--
-- WARNING: Strip off the embedded certificate portion of the following
--          file, if any, prior to executing this UPDATE statement.
--
-- UPDATE Scripts SET Text = readfile('secureTest8.eagle')
-- WHERE Id = '10983482-5c19-4230-8e28-852fa9480624';

INSERT OR REPLACE INTO Scripts (
  Id, CertificateId, Text
) VALUES (
  'cb7fe953-88c7-4df4-914a-395832e1ebb8',
  'd15f3455-1032-4b56-a2d2-b29dcddbceb7',
  '' -- NOT NULL, Replace with "secureTest9.eagle" content.
);

--
-- WARNING: Strip off the embedded certificate portion of the following
--          file, if any, prior to executing this UPDATE statement.
--
-- UPDATE Scripts SET Text = readfile('secureTest9.eagle')
-- WHERE Id = 'cb7fe953-88c7-4df4-914a-395832e1ebb8';

--
-- Insert our "sample" license subscription data.
--

INSERT OR REPLACE INTO Subscriptions (
  Id, CertificateId, Duration, Active, Expires
) VALUES (
  '1935f24a-18ee-49d9-9458-555bf2f39ba5',
  '1b40f0fe-a2fc-4c42-a644-45a851e1c75d',
  '30.00:00:00', 1, '2030-01-01 00:00:00'
);

--
-- Insert our "sample" script subscription data.
--

INSERT OR REPLACE INTO Subscriptions (
  Id, CertificateId, Duration, Active, Expires
) VALUES (
  '609aa5e5-6a03-447a-96b5-fec88dacd3cf',
  '7e9ea823-6f37-484a-917f-ebe4efde7e3b',
  '7.00:00:00', 1, '2030-01-01 00:00:00'
);

INSERT OR REPLACE INTO Subscriptions (
  Id, CertificateId, Duration, Active, Expires
) VALUES (
  '086988bd-da6e-4ec5-880e-7d2970aa7cb9',
  'd15f3455-1032-4b56-a2d2-b29dcddbceb7',
  '7.00:00:00', 1, '2030-01-01 00:00:00'
);

--
-- Insert our "sample" support data.
--

INSERT OR REPLACE INTO Support (
  Id, CertificateId, Active, Expires
) VALUES (
  '22e1976a87ab1b44ee5e49349f0e1654b5b7170025acce8f292b8768016eedea' ||
  '70d0079db13c1c2e3d41e628b14bab1686c2b1cab1949ec9b426f20db25dd92a',
  '1b40f0fe-a2fc-4c42-a644-45a851e1c75d', 1, '2030-01-01 00:00:00'
);
