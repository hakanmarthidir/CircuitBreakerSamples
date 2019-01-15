using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hystrix.Dotnet;
using Hystrix.Dotnet.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CircuitBreaker.HystrixSample
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        //Hystrix 
        //Appsetings içerisinden Circuit Breaker davranışlarını configure edebilecegimiz bir sistemdir. 
        //Her circuit breaker kendi timeout, fallback ve error thresholduna sahip olabilir. 

        public IConfiguration Configuration { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            services.AddHttpClient();
            services.AddHystrix();
            services.Configure<HystrixOptions>(options => Configuration.GetSection("Hystrix").Bind(options));
        }
       
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {            
            app.UseMvc();
            app.UseHystrixMetricsEndpoint("hystrix.stream");
        }
    }
}
