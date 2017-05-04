# InfoCarrier.Core

| branch | package | AppVeyor |
| --- | --- | --- |
| `master` | [![NuGet Badge](https://buildstats.info/nuget/InfoCarrier.Core?includePreReleases=true)](http://www.nuget.org/packages/InfoCarrier.Core) | - |
| `develop` | - | [![Build status](https://ci.appveyor.com/api/projects/status/7jd134yd7m2w035h/branch/develop?svg=true)](https://ci.appveyor.com/project/azabluda/infocarrier-core/branch/develop) |



### Description:
InfoCarrier.Core is a framework developed by [on/off it-solutions gmbh](http://www.onoff-it-solutions.info) for building multitier applications in .NET. This repository contains the key data access component of the framework which essentially is a non-relational provider for [Entity Framework Core](https://github.com/aspnet/EntityFramework) which can be deployed on the client-side of your 3-tier application allowing you to use the full power of EF.Core right in your client application (e.g. WPF, WinForms, etc). The main idea is that instead of querying the relational database the commands are translated into requests to your application server where they are executed against the real database.

It is important to note that InfoCarrier.Core dictates neither the communication platform nor serialization framework. We had positive experience with [WCF](https://msdn.microsoft.com/en-us/library/ms731082.aspx) and [Json.NET](http://www.newtonsoft.com/json), but you are free to choose other frameworks and libraries. InfoCarrier.Core is only responsible for translating client commands into serializable objects, leaving it up to you how to deliver them to the server for actual execution. The same is valid for the execution results.

### Features:
* All features of Entity Framework Core in your client application
  * LINQ
  * Change Tracking
  * Identity Map
  * Navigation Property Fix-up
  * Eager/Explicit Loading of Navigation Properties
  * etc.
* DbContext and entity classes shared between client and server, no need to duplicate this code
* Concise client-side interface `IInfoCarrierBackend`
* Easy to use server-side classes `QueryDataHelper` and `SaveChangesHelper`
* You decide what communication platform to use

### Credits:
InfoCarrier.Core is bringing together the following open source projects
* [Entity Framework Core](https://github.com/aspnet/EntityFramework) by Microsoft.
* [Remote.Linq](https://github.com/6bee/Remote.Linq) and [aqua-core](https://github.com/6bee/aqua-core) by Christof Senn. You will notice that we are directly using classes `Remote.Linq.Expressions.Expression` and `Aqua.Dynamic.DynamicObject` from these two libraries.

## Sample

### Entities and DbContext

This code/assembly can be shared between client and server
```C#
public class Blog { ... }

public class Post { ... }

public class User { ... }

public class BloggingContext : DbContext
{
    public BloggingContext(DbContextOptions options)
        : base(options)
    { }

    public DbSet<Blog> Blogs { get; set; }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder) { ... }
}
```

### Client

Implement `IInfoCarrierBackend` interface, e.g. using Windows Communication Foundation
```C#
public class WcfBackendImpl : IInfoCarrierBackend
{
    private readonly ChannelFactory<IMyRemoteService> channelFactory
        = new ChannelFactory<IMyRemoteService>(...);
    
    public IEnumerable<Aqua.Dynamic.DynamicObject> QueryData(Remote.Linq.Expressions.Expression rlinq)
    {
        IMyRemoteService channel = this.channelFactory.CreateChannel()
        using ((IDisposable)channel)
        {
            return channel.ProcessQueryDataRequest(rlinq);
        }
    }

    public InfoCarrier.Core.Common.SaveChangesResult SaveChanges(IReadOnlyList<IUpdateEntry> entries)
    {
        var request = new InfoCarrier.Core.Common.SaveChangesRequest(entries);

        IMyRemoteService channel = this.channelFactory.CreateChannel()
        using ((IDisposable)channel)
        {
            return channel.ProcessSaveChangesRequest(request);
        }
    }

    // Other methods of IInfoCarrierBackend may just throw NotImplementedException for now
    ...
}
```

Configure and use Entity Framework Core
```C#
var optionsBuilder = new DbContextOptionsBuilder();
optionsBuilder.UseInfoCarrierBackend(new WcfBackendImpl());

using (var context = new BloggingContext(optionsBuilder.Options))
{
    Post myBlogPost = (
        from blog in context.Blogs
        from post in blog.Posts
        join owner in context.Users on blog.OwnerId equals owner.Id
        where owner.login == "hi-its-me"
        where post.Title == "my-blog-post"
        select post).Single();
    
    myBlogPost.Date = DateTime.Now;

    context.SaveChanges();
}
```

### Server

Use `QueryDataHelper` and `SaveChangesHelper` classes to implement the backend service. Without transaction support it can be made very simple and virtually stateless.

```C#
[ServiceContract]
public interface IMyRemoteService
{
    IEnumerable<DynamicObject> ProcessQueryDataRequest(Remote.Linq.Expressions.Expression rlinq);
    
    SaveChangesResult ProcessSaveChangesRequest(SaveChangesResult request);
}

[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
public class MyRemoteService : IMyRemoteService
{
    private DbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseSqlServer(connectionString);
        return new BloggingContext(optionsBuilder.Options);
    }

    public IEnumerable<DynamicObject> ProcessQueryDataRequest(Remote.Linq.Expressions.Expression rlinq)
    {
        using (var helper = new QueryDataHelper(this.CreateDbContext, rlinq))
        {
            return helper.QueryData();
        }
    }
    
    public SaveChangesResult ProcessSaveChangesRequest(SaveChangesResult request)
    {
        using (var helper = new SaveChangesHelper(this.CreateDbContext, request))
        {
            return helper.SaveChanges();
        }
    }
}
```
