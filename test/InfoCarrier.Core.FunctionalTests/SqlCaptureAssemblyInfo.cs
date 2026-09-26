// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;

// Tiers B and C run SQL, so their classes are captured and, once a class has a .wire.sql file, each
// of its tests is compared with its entry in a normal run (#167). See SqlCapture.
[assembly: SqlCapture("InfoCarrier.Core.FunctionalTests.Sqlite", "InfoCarrier.Core.FunctionalTests.Firebird")]
