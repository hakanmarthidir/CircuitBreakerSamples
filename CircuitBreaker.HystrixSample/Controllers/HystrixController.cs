using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Hystrix.Dotnet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CircuitBreaker.HystrixSample.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HystrixController : ControllerBase
    {

        private readonly IHystrixCommand hystrixCommand;
        private readonly IHttpClientFactory httpClientFactory;

        public HystrixController(IHystrixCommandFactory hystrixCommandFactory, IHttpClientFactory httpClientFactory)
        {
            this.hystrixCommand = hystrixCommandFactory.GetHystrixCommand("GroupKey", "CommandKey");
            this.httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public List<StudentMap> Get()
        {
            var result = hystrixCommand.Execute<List<StudentMap>>(() => Run() , () => FallBackMethod(), null );

            return result;
        }

        private List<StudentMap> FallBackMethod()
        {
            return new List<StudentMap>() { new StudentMap() { Name = "fallback name", Surname = "fallback surname" } };
        }

        private List<StudentMap> Run()
        {
            var client = httpClientFactory.CreateClient();
            var response = client.GetAsync("http://localhost:1157/api/values").Result;
            var studentList = response.Content.ReadAsAsync<List<StudentMap>>().Result;
            return studentList;
        }
    }
}