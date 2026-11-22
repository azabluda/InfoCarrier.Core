// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using Dashboard = Microsoft.EntityFrameworkCore.CustomConvertersTestBase<InfoCarrier.Core.CosmosTests.CustomConvertersTest.CosmosFixture>.Dashboard;

namespace InfoCarrier.Core.CosmosTests;

[WireFreeControl]
public class CustomConverterControlTest(CustomConvertersTest.CosmosFixture fixture)
    : IClassFixture<CustomConvertersTest.CosmosFixture>
{
    [StoreLimit]
    [Fact]
    public void Composition_over_converted_collection_refuses_translation()
    {
        using var context = ((InfoCarrierTestStore)fixture.TestStore).Backend.CreateDbContext();
        var query = context.Set<Dashboard>().AsNoTracking().Select(d => new
        {
            d.Id,
            d.Name,
            Layouts = d.Layouts.Select(l => new { H = l.Height, W = l.Width }).ToList()
        });
        var error = Assert.Throws<InvalidOperationException>(() => query.ToList());
        Assert.Equal(
            CoreStrings.TranslationFailed("l => new {     H = l.Height,     W = l.Width }"),
            error.Message.Replace("\r", "").Replace("\n", ""));
    }
}
