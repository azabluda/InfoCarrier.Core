// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;

// Marks each test's start in server-sql.log, for eng/ef-sql-diff.py. Inert unless
// INFOCARRIER_SERVER_SQL is set; see ServerSqlTestMarkerAttribute.
[assembly: ServerSqlTestMarker]
