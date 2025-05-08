using MhpdCommon.Extensions;
using MhpdCommon.Models.MHPDModels;
using MhpdCommon.Repository;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using PensionsRetrievalFunction.Orchestration;
using PensionsRetrievalFunction.Repository;

var host = new HostBuilder()
    .ConfigureOpenApi()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddMhpdCosmosDb(hostContext.Configuration);
        services.AddMhpdUtilities();
        services.AddMhpdHttpClients(hostContext.Configuration);
        services.AddIntegrationServices();
        services.AddMhpdServiceBusTools(hostContext.Configuration);
        services.AddCommonConfigurations(hostContext.Configuration);
        services.AddScoped<IPensionRetrievalRepository, PensionRetrievalRepository>();
        services.AddTransient<IPeiIntegrationOrchestrator, PeiIntegrationOrchestrator>();
        
        services.AddTransient<ICosmosDbRepository<UserSessionData>, UserSessionDataRepository>();

        services.AddSingleton<IOpenApiConfigurationOptions>(_ =>
        {
            var options = new OpenApiConfigurationOptions()
            {
                Info = new OpenApiInfo()
                {
                    Title = "MaPS Pensions Retrieval Records",
                    Version = DefaultOpenApiConfigurationOptions.GetOpenApiDocVersion(),
                    Description =
                        "This service allows a client to retrieve pensions retrieval records for a pension owner session.",
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
                },
                OpenApiVersion = Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums.OpenApiVersionType.V3
            };

            return options;
        });
    })
    .Build();

host.Run();
