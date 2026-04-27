using MhpdCommon.Extensions;
using MhpdCommon.Models.MHPDModels;
using MhpdCommon.Models.OpenApi;
using MhpdCommon.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using PensionsRetrievalFunction.Orchestration;
using PensionsRetrievalFunction.Repository;
using System.Reflection;

var host = CreateHost();
await host.RunAsync();

IHost CreateHost()
{
    var statrupConfiguration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    var useFunctionsApplicationBuilder = statrupConfiguration.GetValue("USE_FUNCTIONSAPPLICATION_BUILDER", false);
    Console.WriteLine($"USE_FUNCTIONSAPPLICATION_BUILDER: {useFunctionsApplicationBuilder}");
    if (useFunctionsApplicationBuilder)
    {
        // LB: Due to a performance issue where all http requests are an order of magnitude slower, we are not currently able to fully support the use of FunctionsApplication
        // However generating swagger doc is no longer maintained by the legacy package and must be done using the new builder.
        // For the moment, FunctionsApplication is only used by the build pipeline. This should be revisited in the future when the performance issue is resolved and we can remove the legacy builder.
        var builder = FunctionsApplication.CreateBuilder([]);
        builder.ConfigureFunctionsWebApplication();
        RegisterServices(builder.Services, builder.Configuration);
        builder.ConfigureAspNetCoreMvcIntegration(mvcBuilder =>
            {
                mvcBuilder.AddMvcOptions(mvcOptions => { });
            })
            .UseAspNetCoreMiddleware(app =>
            {
                app.UseFunctionSwaggerUI();
                app.UseSwagger(c => c.OpenApiVersion = OpenApiSpecVersion.OpenApi2_0);
                app.UseSwaggerUI();
            });

        return builder.Build();
    }
    else
    {
        return new HostBuilder()
            .ConfigureFunctionsWebApplication()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                RegisterServices(services, hostContext.Configuration);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.AddMhpdTelemetry(context.Configuration);
            })
            .Build();

    }
}

void RegisterServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddApplicationInsightsTelemetryWorkerService();
    services.ConfigureFunctionsApplicationInsights();

    services.AddMhpdRedis(configuration);
    services.AddMhpdUtilities(configuration);
    services.AddMhpdHttpClients(configuration);
    services.AddIntegrationServices();
    services.AddMhpdServiceBusTools(configuration);
    services.AddCommonConfigurations(configuration);

    services.AddScoped<IPensionRetrievalRepository, PensionRetrievalRepository>();
    services.AddTransient<IPeiIntegrationOrchestrator, PeiIntegrationOrchestrator>();
    services.AddTransient<ICosmosDbRepository<UserSessionData>, UserSessionDataRepository>();

    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(c =>
    {
        c.EnableAnnotations();
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MaPS Pensions Retrieval Records",
            Description = "This service allows a client to retrieve pensions retrieval records for a pension owner session",
            Contact = new OpenApiContact
            {
                Name = "General Enquires",
                Email = "contact@maps.org.uk",
                Url = new Uri("https://maps.org.uk/en/about-us/contact-us")
            },
            License = new OpenApiLicense
            {
                Name = "Government API License",
                Url = new Uri("https://www.nationalarchives.gov.uk/doc/open-government-licence/version/3/")
            },
        });
        c.DocumentFilter<PensionDataOpenApiFilter>(Assembly.GetExecutingAssembly());
        c.DocumentFilter<PensionDataOpenApiFilter>(Assembly.GetExecutingAssembly());
        c.AddServer(new OpenApiServer
        {
            Url = configuration.GetValue<string>("OpenApiServerUrl") ?? "http://localhost:7123"
        });
    });
}