// Licensed under the MIT license. See license.txt file in the project root for license information.

using FirebirdSql.EntityFrameworkCore.Firebird.Metadata;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Firebird.Query;

/// <summary>
///     <c>UdfDbFunctionTestBase</c> on ADR-009 <b>Tier C</b>, which exists for this base.
/// </summary>
/// <remarks>
///     <para>
///         <b>It ran on Tier B until R157 and could never finish there.</b> About thirty
///         <c>HasDbFunction</c> mappings, of which four are <em>table-valued</em>, and SQLite has
///         no table-valued function and cannot be given one. Eleven tests failed with
///         <c>no such table</c>, and fourteen more never reached the store at all because their
///         correlated form needs <c>APPLY</c>, which SQLite also lacks. Firebird has both: a
///         selectable stored procedure is queried exactly as a table-valued function is, and
///         <c>LATERAL</c> has been in the engine since version 4.
///     </para>
///     <para>
///         <b>The fixture creates every routine the base names, which Firebird's own fixture does
///         not.</b> That provider's <c>UdfDbFunctionFbTests</c> omits
///         <c>GetCustomerOrderCountByYear</c> and its <c>OnlyFrom2000</c> sibling and skips nine
///         tests as "does not have the data". Those are its choices, not the store's limits, so
///         this seed follows the PostgreSQL fixture instead, which creates all four.
///     </para>
///     <para>
///         <b>Three mappings are re-pointed, and each is a real difference between the stores
///         rather than a workaround.</b> <c>IsDate</c> is mapped <c>IsBuiltIn()</c> by the base,
///         so it would be emitted unquoted and resolve to the upper-case <c>ISDATE</c>, which is
///         not the mixed-case routine this fixture creates. <c>MyCustomLength</c> and
///         <c>StringLength</c> are mapped to SQL Server's <c>len</c>, whose Firebird spelling is
///         <c>char_length</c>. And <c>IdentityString</c> is mapped to schema <c>dbo</c>: Firebird
///         has no schemas before version 6, so the schema is cleared rather than invented. The
///         Firebird provider's own fixture makes all three of these changes.
///     </para>
/// </remarks>
public class UdfDbFunctionInfoCarrierTest(UdfDbFunctionInfoCarrierTest.UdfDbFunctionInfoCarrierFixture fixture)
    : UdfDbFunctionTestBase<UdfDbFunctionInfoCarrierTest.UdfDbFunctionInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     The Tier C fixture for EF's user-defined function context.
    /// </summary>
    public class UdfDbFunctionInfoCarrierFixture : UdfFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override string StoreName
            => "UDFDbFunctionInfoCarrierTests";

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                FirebirdInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         <b>Both sides build this, which is why the key generation is set here and not
        ///         in a store-only hook.</b> The harness runs one <c>OnModelCreating</c> on the
        ///         client and the server, so an annotation added here reaches both models. The
        ///         Firebird strategy annotation is read only by the server's DDL generator; the
        ///         client already knows the key is store-generated, because EF's core convention
        ///         says so for an integer primary key on either provider.
        ///     </para>
        ///     <para>
        ///         <b>Key generation has to be asked for.</b> The base assigns no keys and its
        ///         assertions name <c>Id == 1</c> and <c>Id == 2</c>, so the store must generate
        ///         them. Firebird has no implicit identity: a sequence and a trigger are what the
        ///         provider emits when a property says so, and nothing says so by default.
        ///     </para>
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            base.OnModelCreating(modelBuilder, context);

            Type contextType = ContextType;

            Retranslate(nameof(UDFSqlContext.IsDateStatic), "IsDate", builtIn: false);
            Retranslate(nameof(UDFSqlContext.IsDateInstance), "IsDate", builtIn: false);
            Retranslate(nameof(UDFSqlContext.MyCustomLengthStatic), "char_length", builtIn: true);
            Retranslate(nameof(UDFSqlContext.MyCustomLengthInstance), "char_length", builtIn: true);
            Retranslate(nameof(UDFSqlContext.StringLength), "char_length", builtIn: true);

            // Firebird has no schemas before version 6, so `dbo` cannot be created. The routine is
            // an ordinary unqualified one here.
            modelBuilder
                .HasDbFunction(contextType.GetMethod(nameof(UDFSqlContext.IdentityString))!)
                .HasSchema(null);

            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.FindPrimaryKey() is { Properties: [IMutableProperty key] }
                    && key.GetValueGenerationStrategy() == FbValueGenerationStrategy.None)
                {
                    key.SetValueGenerationStrategy(FbValueGenerationStrategy.SequenceTrigger);
                }
            }

            // `builtIn` picks the constructor, and the two differ in exactly one thing: a built-in
            // name is emitted bare, a non-built-in one is delimited. `char_length` is Firebird's
            // own and must stay bare; `IsDate` is this fixture's own routine, created with mixed
            // case, and a bare reference would fold to upper case and not find it.
            void Retranslate(string methodName, string storeName, bool builtIn)
            {
                System.Reflection.MethodInfo method = contextType.GetMethod(methodName)!;
                modelBuilder
                    .HasDbFunction(method)
                    .HasTranslation(args => builtIn
                        ? new SqlFunctionExpression(
                            storeName,
                            args,
                            nullable: true,
                            argumentsPropagateNullability: args.Select(_ => true).ToList(),
                            method.ReturnType,
                            typeMapping: null)
                        : new SqlFunctionExpression(
                            schema: null,
                            storeName,
                            args,
                            nullable: true,
                            argumentsPropagateNullability: args.Select(_ => true).ToList(),
                            method.ReturnType,
                            typeMapping: null));
            }
        }

        /// <inheritdoc />
        /// <remarks>
        ///     <para>
        ///         <c>UdfFixtureBase.SeedAsync</c> only <em>stages</em> its entities: its last
        ///         statements are <c>AddRange</c> calls and it never saves. Every provider fixture
        ///         is expected to finish the job with its own routine definitions and a save, as
        ///         EF's SqlServer fixture does. Omitting the save is not a quiet failure but an
        ///         empty store, and it once cost this repository a whole wrong classification.
        ///     </para>
        ///     <para>
        ///         <b>Firebird spells a table-valued function as a selectable stored
        ///         procedure.</b> It declares output parameters, loops, and <c>SUSPEND</c>s a row
        ///         at a time; EF maps it through <c>HasDbFunction</c> without knowing the
        ///         difference, and queries it as <c>SELECT ... FROM "Name"(args)</c>. Each
        ///         statement goes in its own call because Firebird's parser takes one at a time.
        ///     </para>
        /// </remarks>
        protected override async Task SeedAsync(DbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            await base.SeedAsync(context);
            await context.SaveChangesAsync();

            foreach (string statement in Routines)
            {
                await context.Database.ExecuteSqlRawAsync(statement);
            }
        }

        /// <summary>
        ///     Every routine the base names, with the semantics of the <c>create function</c>
        ///     statements EF's SqlServer fixture runs.
        /// </summary>
        private static IEnumerable<string> Routines =>
        [
            """
            create function "CustomerOrderCount" (customerId int)
            returns int
            as
            begin
                return (select count("Id") from "Orders" where "CustomerId" = :customerId);
            end
            """,

            // One definition, where SQL Server has two overloads. Firebird has no routine
            // overloading, and the second argument's only use is to be concatenated, so a
            // character parameter answers the integer call as well.
            """
            create function "StarValue" (starCount int, val varchar(1000))
            returns varchar(1000)
            as
            begin
                return rpad('', :starCount, '*') || :val;
            end
            """,

            """
            create function "DollarValue" (starCount int, val varchar(1000))
            returns varchar(1000)
            as
            begin
                return rpad('', :starCount, '$') || :val;
            end
            """,

            // The period is ignored, as it is in EF's SqlServer fixture.
            """
            create function "GetReportingPeriodStartDate" (period int)
            returns timestamp
            as
            begin
                return cast('1998-01-01' as timestamp);
            end
            """,

            """
            create function "GetCustomerWithMostOrdersAfterDate" (searchDate timestamp)
            returns int
            as
            begin
                return (select first 1 "CustomerId"
                        from "Orders"
                        where "OrderDate" > :searchDate
                        group by "CustomerId"
                        order by count("Id") desc);
            end
            """,

            """
            create function "IsTopCustomer" (customerId int)
            returns boolean
            as
            begin
                return :customerId = 1;
            end
            """,

            """
            create function "IdentityString" (customerName varchar(1000))
            returns varchar(1000)
            as
            begin
                return :customerName;
            end
            """,

            """
            create function "IsDate" (val varchar(1000))
            returns boolean
            as
            declare dummy date;
            begin
                begin
                    begin
                        dummy = cast(:val as date);
                    end
                    when any do
                    begin
                        return false;
                    end
                end
                return true;
            end
            """,

            """
            create function "AddValues" (a int, b int)
            returns int
            as
            begin
                return :a + :b;
            end
            """,

            // ---- The four table-valued ones. This is what Tier B could not host. ----

            """
            create procedure "GetCustomerOrderCountByYear" (customerId int)
            returns ("CustomerId" int, "Count" int, "Year" int)
            as
            begin
                for select "CustomerId", count("Id"), extract(year from "OrderDate")
                    from "Orders"
                    where "CustomerId" = :customerId
                    group by "CustomerId", extract(year from "OrderDate")
                    order by extract(year from "OrderDate")
                into :"CustomerId", :"Count", :"Year" do
                begin
                    suspend;
                end
            end
            """,

            """
            create procedure "GetCustomerOrderCountByYearOnlyFrom2000" (customerId int, onlyFrom2000 boolean)
            returns ("CustomerId" int, "Count" int, "Year" int)
            as
            begin
                for select :customerId, count("Id"), extract(year from "OrderDate")
                    from "Orders"
                    where "CustomerId" = 1
                      and (:onlyFrom2000 is null
                           or :onlyFrom2000 = false
                           or extract(year from "OrderDate") = 2000)
                    group by extract(year from "OrderDate")
                    order by extract(year from "OrderDate")
                into :"CustomerId", :"Count", :"Year" do
                begin
                    suspend;
                end
            end
            """,

            """
            create procedure "GetTopTwoSellingProducts"
            returns ("ProductId" int, "AmountSold" int)
            as
            begin
                for select first 2 "ProductId", sum("Quantity")
                    from "LineItem"
                    group by "ProductId"
                    order by sum("Quantity") desc
                into :"ProductId", :"AmountSold" do
                begin
                    suspend;
                end
            end
            """,

            """
            create procedure "GetOrdersWithMultipleProducts" (customerId int)
            returns ("OrderId" int, "CustomerId" int, "OrderDate" timestamp)
            as
            begin
                for select o."Id", :customerId, o."OrderDate"
                    from "Orders" o
                    join "LineItem" li on o."Id" = li."OrderId"
                    where o."CustomerId" = :customerId
                    group by o."Id", o."OrderDate"
                    having count(li."ProductId") > 1
                into :"OrderId", :"CustomerId", :"OrderDate" do
                begin
                    suspend;
                end
            end
            """,
        ];
    }
}
