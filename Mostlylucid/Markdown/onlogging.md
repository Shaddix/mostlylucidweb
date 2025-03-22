# On Logging in ASP.NET Applications (Part 1...probably)

<!--category-- Logging,Serilog,ASP.NET,C# -->
<datetime class="hidden">2024-10-27T17:00</datetime>
# Introduction
Logging is OF COURSE a critical part of applications however I often see it misunderstood / misused in ASP.NET applications. This is part post and part manifesto on how to effectively log in ASP.NET Applications.

This is not a complete guide to logging; there's LOTS of those out there; it's more about choosing an effective logging strategy for your ASP.NET applications.

In principal logging should be USEFUL; whether it's logging while you're developing or logging in production it should be useful to you / your team / your users.

[TOC]

# The Problem
Too often logging is seen as the 'only do it  when there's exceptions' thing. I think this misunderstands what logging should be.

**LOGGING IS NOT JUST FOR EXCEPTIONS**

In general, you should be able to run a significant part of your application locally; in this context `log.LogInformation`  is your friend.

# The Solution
## Logging Success
Over the 30 years I've written code I've seen a few examples of good logging And WAY more examples of poor logging. 

Principles of good logging:
1. Logging in Development should give sufficient information about how your application is running to understand what's happening when.
2. Logging in Production should give you a super quick and easy mechanism to understand how your application fails.
3. TOO MUCH logging in production is a negative; it can slow down your application and make it harder to find the important stuff.

Remember; logging ALWAYS drags down your application performance; you should log what you need to diagnose an issue but no more in production / where performance is critical (e.g., don't run a performance test when using full Debug Logging).

## Types of Logging In ASP.NET

### Serilog vs Microsoft.Extensions.Logging
In ASP.NET you already have a build in logging System; `Microsoft.Extensions.Logging`. This is a good logging system, but it's not as flexible as Serilog. Serilog is a more flexible logging system that allows you to log to multiple sinks (outputs) and enrich your logs with additional information.

### BootStrap Logging
This is the logging that happens when your application starts up. It's the first thing that happens, and it's the first thing you should see when you run your application. In ASP.NET especially when you use configuration it's critical you understand what's happening when your application starts up. It's a super common source of issues in ASP.NET applications.

For instance in Serilog you can do this:
```csharp
Log.Logger = new LoggerConfiguration()
             .WriteTo.Console()
             .WriteTo.File("logs/boot-*.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
             .CreateBootstrapLogger();
```
*NOTE: This is limited as you don't have access to the configuration at this point so for example to use AppInsights you'd likely need an `#if RELEASE` block to set the appropriate key .*

What this does is give you data about any startup issues in your application; it writes both to console and to a file. It's also important to ensure you don't save ALL the log files; you can see here we're only saving 7 days worth of logs.

You'll also want to wrap your startup method (in this app I use the new `Program.cs` method) in a try/catch and log any exceptions that happen there.

```csharp
try
 {
        ..all your startup code
 }
catch (Exception ex)
{
    if(args.Contains("migrate"))
    {
        Log.Information("Migration complete");
        return;
    }
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```
The Log.CloseAndFlush() is important as it ensures all the logs are written to disk (your app is crashing so otherwise it will exit before it does this).

Here I also detect whether the app is running in a migration mode and if so I log that and exit.

`Log.Fatal` is the most severe log level; it's used when your application is about to crash.
*Not All Exceptions are Fatal* - you should use `Log.Error` for those (unless they mean the APP (not the request) cannot continue past that point).


## Log Levels
In ASP.NET (and .NET More generally) you have a number of log levels:
1. `Log.Fatal` - This is the most severe log level; it's used when your application is about to crash.
2. `Log.Error` - This is used when an exception happens that means the APP (not the request) cannot continue past that point.
3. `Log.Warning` - This is used when something happens that's not an error but is something you should be aware of (for instance a degraded service / something *not quite right* but not an error).
4. `Log.Information` - This is used for general information about what's happening in your application.
5. `Log.Debug` - This is used for information that's only useful when you're debugging your application (it's EVERYWHERE in development but should be turned off in production).

For production I'd generally set the logging to be `Log.Fatal`, `Log.Error` and `Log.Warning` only.

## Runtime Logging
As opposed to startup logging this is the logging that happens when your application is running. This is the logging that tells you what's happening in your application at any given time.


For instance to use these in Serilog you'd do this:
```json
  "Serilog": {
    "Enrich": ["FromLogContext", "WithThreadId", "WithThreadName", "WithProcessId", "WithProcessName", "FromLogContext"],
    "MinimumLevel": "Warning",
    "WriteTo": [
        {
          "Name": "Seq",
          "Args":
          {
            "serverUrl": "http://seq:5341",
            "apiKey": ""
          }
        },
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/applog-.txt",
          "rollingInterval": "Day"
        }
      }

    ],
    "Properties": {
      "ApplicationName": "mostlylucid"
    }
  },
```

## Enriches
As you'll see I'm quite a big fan of Serilog; it's a great logging library that's very flexible. One of the things I like about it is the ability to enrich your logs with additional information. This can be very useful in diagnosing issues in your application.
In the above example you can see I'm enriching my logs with the thread id, thread name, process id and process name. This can be very useful in diagnosing issues in your application.

I also set the logging to go to multiple 'sinks' (outputs); in this case I'm sending it to the console, to a file and to Seq (a logging service).

Having multiple sinks is handy in case one fails, in this case my preferred logging service is Seq, so I send it there first. Then if that fails I send it to the console and to a file.

I also set the ApplicationName property in the logs; this can be useful if you have multiple applications logging to the same service (which for distributed applications you often will; along with a correlation ID to tie the logs together for a single request). 

For example if you have a JS front end and a .NET backend you can set the correlation ID in the front end and pass it to the backend. This allows you to see all the logs for a single request in one place. OR if you use HttpClient in your backend you can pass the correlation ID in the headers to the other service.

There's great coverage on how you could do this here: https://josef.codes/append-correlation-id-to-all-log-entries-in-asp-net-core/ but I'll cover it in a future post.

## Structured Logging
Structured logging is a way of logging that makes it easier to search and filter your logs. In Serilog you can do this by using the `Log.Information("This is a log message with {Property1} and {Property2}", property1, property2);` syntax. This makes it easier to search for logs with a particular property.

## Contextual Logging
Using Serilog you can also use `LogContext.PushProperty` to add properties to your logs that are only relevant for a particular scope. This can be very useful in diagnosing issues in your application.

## Serilog Setup
Serilog is super flexible in that it allows you to configure it in code as well as through config. 
For instance to use the configuration above you'd do this:
```csharp
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .MinimumLevel.Warning()
        .Enrich.FromLogContext()
        .Enrich.WithThreadId()
        .Enrich.WithThreadName()
        .Enrich.WithProcessId()
        .Enrich.WithProcessName()
        .WriteTo.Seq("http://seq:5341", apiKey: "")
        .WriteTo.Console()
        .WriteTo.File("logs/applog-.txt", rollingInterval: RollingInterval.Day)
        .Enrich.WithProperty("ApplicationName", "mostlylucid");
});

```
This is the same as the configuration above but in code. This is useful if you want to set up your logging in code; it's really up to you how you do it.


# Exception Filters
In ASP.NET you can use Exception Filters to catch exceptions and log them. This is a great way to ensure you log all exceptions in your application.
HOWEVER it's not a replacement for method level logging; you should still log at the method level so you can see what's happening in your application in detail; the Exception filter just shows what happens once the whole request stack has been unwound and can often be very confusing to work out what's happened.

This changed in ASP.NET Core 8 and there's a TON you can do with these [new features](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-8.0).

Bug again the same principles apply; you should log at the method level so you can see what's happening in your application in detail; the Exception filters / status handlers just show what happens once the whole request stack has been unwound and can often be very confusing to work out what's happened.

# DON'T LOG EVERYTHING IN PROD
Logging should be appropriate to the environment; in Development you log everything so you can see what is happening at each point in your application but in Test you need less logging (maybe log Information here) and in Production you need even less (Warning and above).

It's tempting to log everything in production but it's a bad idea. You should log everything you need to diagnose an issue but no more. In cloud providers you can easily get charged for the amount of data you send to the logging service so you should be careful about what you log and how long you retain these logs for (Application Insights stores logs for 90 days by default; this is often TOO LONG and will cost you  for as long as that log data is stored). 

For a very 'active' app you just won't need long term logging of that data; you'll have a good idea of what's happening in the app from the last few days of logs.

## Request Logging
Once common issue I see in production is logging failed requests; while these can be interesting (nobody wants a 404 / 401) these can be a huge amount of data and can be very expensive to store. You should only log things *you intend to fix*, for a 404 it may be that this is important as it can indicate a broken link in your application; for a 401 it may be that this is important as it can indicate a broken authentication system.

HOWEVER as I mentioned above these are better handled in the code they actually happen in (in the case of 401); it may be that these should also be logged as a `Warning` or `Error` in the code that actually generates the 401. 

To set up Request Logging in Serilog you can do this:
```csharp
app.UseSerilogRequestLogging();
```
This will log all requests to your application; it's a great way to see what's happening in your application at a high level; but again you should be careful about what you log and how long you retain these logs for.

For Application Insights you may still want to log using Information level as it will give you the nifty User Journey feature; this is a great way to see how users are using your application and can be very useful in diagnosing issues; however again these quickly rack up costs so you should be careful about what you log and how long you retain these logs for.

# In Conclusion
So that's it, a quick run through of how to log in ASP.NET applications. Much of it is a reaction to over-zealous, useless logs I see in many production applications. 
In development you want to see *how the app runs*; in production you want to see *how the app fails*.
Logging is too often a useless, expensive part of application development. As with everything in software development logging should be focused on the USER; whether its you / other developers while working on the app in your IDE / locally or sorting out issues in production rapidly and efficiently.