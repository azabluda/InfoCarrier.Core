# InfoCarrier.Core

| branch | package | AppVeyor |
| --- | --- | --- |
| `master` | [![NuGet](https://img.shields.io/nuget/v/InfoCarrier.Core.svg?style=flat-square&label=nuget)](https://www.nuget.org/packages/InfoCarrier.Core/) | [![Build status](https://ci.appveyor.com/api/projects/status/7jd134yd7m2w035h/branch/master?svg=true)](https://ci.appveyor.com/project/azabluda/infocarrier-core/branch/master) |
| `develop` | - | [![Build status](https://ci.appveyor.com/api/projects/status/7jd134yd7m2w035h/branch/develop?svg=true)](https://ci.appveyor.com/project/azabluda/infocarrier-core/branch/develop) |



### Description:
InfoCarrier.Core is a framework developed by [on/off it-solutions gmbh](http://www.onoff-it-solutions.info) for building multi-tier applications in .NET. This repository contains the key data access component of the framework which essentially is a non-relational provider for [Entity Framework Core](https://github.com/aspnet/EntityFramework) which can be deployed on the client-side of your 3-tier application allowing you to use the full power of EF.Core right in your client application (e.g. WPF, WinForms, Xamarin, UWP, etc). The main idea is that instead of querying the relational database the commands are translated into requests to your application server where they are executed against the real database.

It is important to note that InfoCarrier.Core dictates neither the communication platform nor serialization framework. We had positive experience with [WCF](https://msdn.microsoft.com/en-us/library/ms731082.aspx) and [Json.NET](http://www.newtonsoft.com/json), but you are free to choose other frameworks and libraries. InfoCarrier.Core is only responsible for translating client commands into serializable objects, leaving it up to you how to deliver them to the server for actual execution. The same is valid for the execution results.

### Features:
* All features of Entity Framework Core in your client application
  * LINQ
  * Change Tracking
  * Identity Map
  * Navigation Property Fix-up
  * Eager/Lazy/Explicit Loading of Navigation Properties
  * etc.
* DbContext and entity classes shared between client and server, no need to duplicate this code
* Concise client-side interface `IInfoCarrierBackend`
* Easy to use server-side classes `QueryDataHelper` and `SaveChangesHelper`
* You decide what communication platform to use

### Credits:
InfoCarrier.Core is bringing together the following open source projects
* [Entity Framework Core](https://github.com/aspnet/EntityFramework) by Microsoft.
* [Remote.Linq](https://github.com/6bee/Remote.Linq) and [aqua-core](https://github.com/6bee/aqua-core) by Christof Senn.

## Sample

The complete WCF sample is located in the [/sample](sample) folder. There you also find simple client/server applications based on [ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/index) Web API and [ServiceStack](https://servicestack.net/) with basic support of transactions.

### Entities and DbContext

This code/assembly can be shared between client and server
```C#
public class Blog { ... }

public class Post { ... }

public class Author { ... }

public class BloggingContext : DbContext
{
    public BloggingContext(DbContextOptions options)
        : base(options)
    { }

    public DbSet<Blog> Blogs { get; set; }

    public DbSet<Author> Authors { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) { ... }
}
```

### Client

Implement `IInfoCarrierBackend` interface, e.g. using Windows Communication Foundation
```C#
public class WcfBackendImpl : IInfoCarrierBackend
{
    private readonly ChannelFactory<IWcfService> channelFactory
        = new ChannelFactory<IWcfService>(...);

    // Gets the remote server address. Used for logging.
    public string ServerUrl
        => this.channelFactory.Endpoint.Address.ToString();

    public QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
    {
        IWcfService channel = this.channelFactory.CreateChannel();
        using ((IDisposable)channel)
        {
            return channel.ProcessQueryDataRequest(request);
        }
    }

    public SaveChangesResult SaveChanges(SaveChangesRequest request)
    {
        IWcfService channel = this.channelFactory.CreateChannel();
        using ((IDisposable)channel)
        {
            return channel.ProcessSaveChangesRequest(request);
        }
    }

    // Let other methods of IInfoCarrierBackend just throw NotSupportedException for now.
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
        join author in context.Authors on blog.AuthorId equals author.Id
        where author.login == "hi-its-me"
        where post.Title == "my-blog-post"
        select post).Single();

    myBlogPost.Date = DateTime.Now;

    context.SaveChanges();
}
```

### Server

Use `QueryDataHelper` and `SaveChangesHelper` classes to implement the back-end service. In the simple case when no transaction support is required it may look like the following:

```C#
[ServiceContract]
public interface IWcfService
{
    [OperationContract]
    QueryDataResult ProcessQueryDataRequest(QueryDataRequest request);

    [OperationContract]
    SaveChangesResult ProcessSaveChangesRequest(SaveChangesRequest request);
}

[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
public class InfoCarrierService : IWcfService
{
    private DbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder();
        optionsBuilder.UseSqlServer(connectionString);
        return new BloggingContext(optionsBuilder.Options);
    }

    public QueryDataResult ProcessQueryDataRequest(QueryDataRequest request)
    {
        using (var helper = new QueryDataHelper(this.CreateDbContext, request))
        {
            return helper.QueryData();
        }
    }

    public SaveChangesResult ProcessSaveChangesRequest(SaveChangesRequest request)
    {
        using (var helper = new SaveChangesHelper(this.CreateDbContext, request))
        {
            return helper.SaveChanges();
        }
    }
}
```
