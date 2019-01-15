using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Fallback;
using Polly.Timeout;

namespace CircuitBreaker.PollySample
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            //Polly içerisinde reactive ve proactive olarak ayrılmıs 2 tur policysi vardır. 
            //reactive policyler => retry ve circuit breaker gibi
            //proactive policyler ise => timeout policy, bulkhead, caching... yüksek verimli sistemler için esnekligi arttırırlar. 
            //Policyler sıra ile uygulanır. 

            var registry = services.AddPolicyRegistry();

            #region RetryPolicy
            //HandleTransientHttpError : 
            //varsayılan olarak alınabilecek errorlar tanımlıdır. HttpRequestException, HTTP 5xx (sunucu hataları), HTTP 408 (istek zaman asımı) gibi hatalar alındıgında retry mekanizması calisir. 
            //Ancak bu hataların dısında da aldıgınız hatalarda retry mekanizmasının calismasını isteyebilirsiniz. O zaman policy e OrResult eklemesi yaparsınız. 

            //Her bir retry da yapılmasını istediginiz bir sey varsa OnRetry ve OnRetryAsync içerisine yazabilirsiniz. Loglama vs. 
            //Retry mekanizmasının iyi insaa edilmesi gerekir. Zaten cevap vermeyen bir sisteme tekrarlı denemeler yapmak ilave yuk getirebilir.
            //cevap veremeyen bir sistemi zorlamak yerine hızlı basarısızlık genellikle daha iyi bir kullanıcı deneyimidir. Circuitbreaker sistemi belli bir sure kapalı tutabilir ve ardından tekrar acarak isteklere cevap verilip verilmeyecegini dener. 

            registry.Add("retryrules", HttpPolicyExtensions.HandleTransientHttpError() // varsayılanlar alındıgında
                .OrResult(response => (int)response.StatusCode == 429) // 429 alındıgında
                .Or<TimeoutRejectedException>() // timeout policye takılıp ilgili exception calistiginda retry mekanizması devreye girer.
                .WaitAndRetryAsync(new[]
            {
                TimeSpan.FromSeconds(3), // ilk denemeyi yapmadan evvel 3 saniye bekler
                TimeSpan.FromSeconds(8)  // ikinci denemeyi yapmadan evvel 8 saniye bekler              
            },
            onRetryAsync: async (outcome, timespan, retryAttempt, context) =>
            {
                System.Diagnostics.Debug.WriteLine(string.Format("deneme {0}", DateTime.Now.ToString()));
            }
            ));

            //registry.Add("retryrules", HttpPolicyExtensions.HandleTransientHttpError() // varsayılanlar alındıgında
            //    .OrResult(response => (int)response.StatusCode == 429) // 429 alındıgında
            //    .Or<TimeoutRejectedException>() // timeout policye takılıp ilgili exception calistiginda retry mekanizması devreye girer.
            //    .WaitAndRetry(new[]
            //{
            //    TimeSpan.FromSeconds(3), // ilk denemeyi yapmadan evvel 3 saniye bekler
            //    TimeSpan.FromSeconds(8)  // ikinci denemeyi yapmadan evvel 8 saniye bekler              
            //},
            //onRetry: (outcome, timespan, retryAttempt, context) =>
            //{
            //    System.Diagnostics.Debug.WriteLine(string.Format("deneme {0}", DateTime.Now.ToString()));
            //}

            //));

            #endregion

            #region CircuitBreaker
            //CircuitBreaker :
            //    failureThreshold: 0.5, (0 ile 1 arasında deger alır. arıza oranı olarak adlandırabiliriz. işlenen isteklerin basarisizlikla sonuclanma oranı %50 ve daha fazla ise sistem kesilir. )
            //    samplingDuration: TimeSpan.FromSeconds(5), // bu sure zarfında gerceklesen işlemler metriclerde kullanılır. daha eskisi kullanılmaz. 5 sanyedeki degerlere bakılır ve yuzde 50 basarisizlik varsa sistem kırılır. bu sure ne kadar uzun olursa o kadar duyarlılık azalır. dusuk olmasında fayda var. izin verilen min deger 20 miliseconds
            //    minimumThroughput: 20, // istatistik için gerekli olan cagrı sayısı. bu sayıya ulastıktan sonra metricler kullanılır ve devre kırılmayı dusunmeye baslar. izin verilen min deger 2 dir.
            //    durationOfBreak: TimeSpan.FromSeconds(30)

            //Advanced Kullanım 
            registry.Add("circuitbreakerrules", HttpPolicyExtensions.HandleTransientHttpError()
                .OrResult(response => (int)response.StatusCode == 429)
                .Or<TimeoutRejectedException>()
                .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(5),
                minimumThroughput: 6,
                durationOfBreak: TimeSpan.FromSeconds(120)
                ));


            //var circuitbreakerPolicy = HttpPolicyExtensions.HandleTransientHttpError()
            //   .OrResult(response => (int)response.StatusCode == 429)
            //   .Or<TimeoutRejectedException>()
            //   .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking: 5, durationOfBreak: TimeSpan.FromSeconds(30)
            //   );
            //registry.Add("circuitbreakerrules", circuitbreakerPolicy);


            #endregion

            #region TimeOutPolicy         
            var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(2);
            registry.Add("timeoutrules", timeoutPolicy); // Belirtilen surede response gelmez ise TimeoutRejectedException throw eder.             
            #endregion

            #region Fallback           

            FallbackPolicy<HttpResponseMessage> fallbackForCircuitBreaker = Policy<HttpResponseMessage>
                .Handle<TimeoutRejectedException>()
                .Or<BrokenCircuitException>()       
                .FallbackAsync(FallbackStudent());


            registry.Add("fallbackrules", fallbackForCircuitBreaker);
            #endregion

            services.AddHttpClient(ClientNames.LocalClient, client =>
            {
                client.BaseAddress = new Uri("http://localhost:1157/api/");
                client.Timeout = TimeSpan.FromSeconds(2);
            })
             .AddPolicyHandlerFromRegistry("fallbackrules")
             .AddPolicyHandlerFromRegistry("timeoutrules")
             //.AddPolicyHandlerFromRegistry("retryrules")
             .AddPolicyHandlerFromRegistry("circuitbreakerrules")             
            ;
        }

        private HttpResponseMessage FallbackStudent()
        {
            var data = Newtonsoft.Json.JsonConvert.SerializeObject(new List<StudentMap>() { new StudentMap() { Name = "fallback name", Surname = "fallback surname" } }, Newtonsoft.Json.Formatting.Indented);
            var response = new HttpResponseMessage()
            {
                Content = new StringContent(data, Encoding.UTF8, "application/json")                
            };           

            return response;
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseMvc();
        }
    }
}
