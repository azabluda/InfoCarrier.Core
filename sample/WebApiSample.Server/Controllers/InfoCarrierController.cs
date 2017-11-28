// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample.Controllers
{
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Server;
    using Microsoft.AspNetCore.Mvc;

    [Route("api")]
    public class InfoCarrierController : Controller
    {
        [HttpPost]
        [Route("QueryData")]
        public async Task<QueryDataResult> PostQueryDataAsync([FromBody] QueryDataRequest request)
        {
            using (var helper = new QueryDataHelper(SqlServerShared.CreateDbContext, request))
            {
                return await helper.QueryDataAsync();
            }
        }

        [HttpPost]
        [Route("SaveChanges")]
        public async Task<SaveChangesResult> PostSaveChangesAsync([FromBody] SaveChangesRequest request)
        {
            using (var helper = new SaveChangesHelper(SqlServerShared.CreateDbContext, request))
            {
                return await helper.SaveChangesAsync();
            }
        }
    }
}
